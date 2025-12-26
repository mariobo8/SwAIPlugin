using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwAIPlugin
{
    /// <summary>
    /// The "Batch Walker" component for automated training data generation.
    /// Iterates through SolidWorks parts, steps through features, and captures
    /// visual/mathematical data for AI training.
    /// </summary>
    public class FeatureWalker
    {
        private SldWorks swApp;
        private string inputFolder;
        private string outputFolder;
        private StringBuilder logBuilder;

        // Feature types to skip (system/internal/folder features)
        private static readonly HashSet<string> SkipFeatureTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // System features
            "OriginProfileFeature",
            "RefAxis",
            "RefPlane",
            "DOC",
            
            // Folder features (not actual geometry)
            "MaterialFolder",
            "SensorFolder",
            "DetailCabinet",
            "HistoryFolder",
            "CommentsFolder",
            "FavoriteFolder",
            "SelectionSetFolder",
            "SurfaceBodyFolder",
            "SolidBodyFolder",
            "CutListFolder",
            "SubWeldFolder",
            "SubAtomFolder",
            "MateGroup",
            "MateReferenceGroupFolder",
            
            // Sketch features - we capture these AS PART of the parent feature, not separately
            "ProfileFeature",
            "3DProfileFeature",
            "Sketch",
            "3DSketch",
            
            // Other system items
            "Selection_Sets",
            "Surface_Bodies", 
            "Solid_Bodies",
            "Lights",
            "Cameras",
            "Ambient",
            "GroundPlane"
        };

        // Feature types that represent actual geometry we want to capture
        private static readonly HashSet<string> GeometryFeatureTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Extrusions (ICE = Cut-Extrude internal name, Extrusion = Boss-Extrude internal name)
            "Boss-Extrude", "Cut-Extrude", "Extrusion", "ICE", "Boss", "Cut",
            
            // Revolves
            "Boss-Revolve", "Cut-Revolve", "Revolution",
            
            // Edge features
            "Fillet", "Chamfer", "ConstRadiusFillet", "VarRadiusFillet",
            
            // Shell and modifications
            "Shell", "Draft", "Dome", "Flex",
            
            // Holes
            "Hole", "HoleWzd", "HoleWizard", "SimpleHole",
            
            // Patterns
            "LPattern", "CirPattern", "MirrorPattern", "LocalLPattern", "LocalCirPattern",
            "DerivedLPattern", "DerivedCirPattern", "VarPattern",
            
            // Sweeps and Lofts
            "Boss-Sweep", "Cut-Sweep", "Sweep",
            "Boss-Loft", "Cut-Loft", "Loft",
            
            // Other geometry
            "Rib", "Wrap", "Thicken", "Boss-Thin", "Cut-Thin",
            "Helix", "Spiral",
            
            // Boundary and surface
            "Boss-Boundary", "Cut-Boundary",
            "SewRefSurface", "Knit"
        };

        public FeatureWalker(SldWorks app, string inputPath, string outputPath)
        {
            swApp = app;
            inputFolder = inputPath;
            outputFolder = outputPath;
            logBuilder = new StringBuilder();
        }

        /// <summary>
        /// Main entry point: Process all .SLDPRT files in the input folder
        /// </summary>
        public string ProcessFolder()
        {
            logBuilder.Clear();
            Log("========================================");
            Log("FEATURE WALKER - Training Data Generator");
            Log("========================================");
            Log($"Started: {DateTime.Now}");
            Log($"Input Folder: {inputFolder}");
            Log($"Output Folder: {outputFolder}");
            Log("");

            // Validate folders
            if (!Directory.Exists(inputFolder))
            {
                Log($"ERROR: Input folder does not exist: {inputFolder}");
                return logBuilder.ToString();
            }

            if (!Directory.Exists(outputFolder))
            {
                try
                {
                    Directory.CreateDirectory(outputFolder);
                    Log($"Created output folder: {outputFolder}");
                }
                catch (Exception ex)
                {
                    Log($"ERROR: Could not create output folder: {ex.Message}");
                    return logBuilder.ToString();
                }
            }

            // Find all .SLDPRT files
            string[] partFiles = Directory.GetFiles(inputFolder, "*.SLDPRT", SearchOption.TopDirectoryOnly);
            
            if (partFiles.Length == 0)
            {
                // Also try lowercase extension
                partFiles = Directory.GetFiles(inputFolder, "*.sldprt", SearchOption.TopDirectoryOnly);
            }

            if (partFiles.Length == 0)
            {
                Log("No .SLDPRT files found in input folder.");
                return logBuilder.ToString();
            }

            Log($"Found {partFiles.Length} part file(s) to process.");
            Log("");

            int successCount = 0;
            int errorCount = 0;

            foreach (string partFile in partFiles)
            {
                try
                {
                    bool result = ProcessPart(partFile);
                    if (result)
                        successCount++;
                    else
                        errorCount++;
                }
                catch (Exception ex)
                {
                    Log($"  EXCEPTION processing {Path.GetFileName(partFile)}: {ex.Message}");
                    errorCount++;
                }
            }

            Log("");
            Log("========================================");
            Log("SUMMARY");
            Log("========================================");
            Log($"Total Parts: {partFiles.Length}");
            Log($"Successful: {successCount}");
            Log($"Errors: {errorCount}");
            Log($"Completed: {DateTime.Now}");

            return logBuilder.ToString();
        }

        /// <summary>
        /// Process a single part file - walk through all features
        /// </summary>
        private bool ProcessPart(string partFilePath)
        {
            string partName = Path.GetFileNameWithoutExtension(partFilePath);
            Log($"Processing: {partName}");

            // Create output directory for this part
            string partOutputDir = Path.Combine(outputFolder, partName);
            if (!Directory.Exists(partOutputDir))
            {
                Directory.CreateDirectory(partOutputDir);
            }

            int errors = 0;
            int warnings = 0;

            // Open the part silently
            ModelDoc2 swModel = swApp.OpenDoc6(
                partFilePath,
                (int)swDocumentTypes_e.swDocPART,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings
            ) as ModelDoc2;

            if (swModel == null)
            {
                Log($"  ERROR: Could not open part (errors={errors}, warnings={warnings})");
                return false;
            }

            try
            {
                // Activate and zoom to fit
                swApp.ActivateDoc3(partFilePath, false, (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref errors);
                swModel.ViewZoomtofit2();

                // Get all features
                List<Feature> geometryFeatures = GetGeometryFeatures(swModel);
                Log($"  Found {geometryFeatures.Count} geometry feature(s)");

                if (geometryFeatures.Count == 0)
                {
                    Log("  WARNING: No geometry features found");
                    return true;
                }

                // Process each feature
                int featureIndex = 0;
                foreach (Feature feat in geometryFeatures)
                {
                    try
                    {
                        ProcessFeature(swModel, feat, partOutputDir, featureIndex);
                        featureIndex++;
                    }
                    catch (Exception ex)
                    {
                        Log($"    ERROR on feature '{feat.Name}': {ex.Message}");
                    }
                }

                Log($"  Successfully processed {featureIndex} feature(s)");
                return true;
            }
            finally
            {
                // Close the document without saving
                swApp.CloseDoc(partFilePath);
            }
        }

        /// <summary>
        /// Get list of geometry features (only actual modeling features, skip system/folders/sketches)
        /// </summary>
        private List<Feature> GetGeometryFeatures(ModelDoc2 swModel)
        {
            List<Feature> features = new List<Feature>();
            Feature feat = swModel.FirstFeature() as Feature;

            while (feat != null)
            {
                string typeName = feat.GetTypeName2();
                string featName = feat.Name;

                // Skip if it's in the skip list
                if (!SkipFeatureTypes.Contains(typeName))
                {
                    // Also skip by name pattern (catches localized folder names)
                    bool skipByName = featName.Contains("Bodies") || 
                                      featName.Contains("Selection") ||
                                      featName.Contains("Folder") ||
                                      featName.Contains("Lights") ||
                                      featName.Contains("Cameras") ||
                                      featName.StartsWith("Sketch") ||  // Skip standalone sketches
                                      featName.StartsWith("3DSketch");
                    
                    // Only add if it's a known geometry feature type
                    if (!skipByName && GeometryFeatureTypes.Contains(typeName))
                    {
                        features.Add(feat);
                        Log($"      Found geometry: {featName} ({typeName})");
                    }
                }

                feat = feat.GetNextFeature() as Feature;
            }

            return features;
        }

        /// <summary>
        /// Process a single feature - capture before/after screenshots, sketch, and metadata
        /// </summary>
        private void ProcessFeature(ModelDoc2 swModel, Feature feat, string partOutputDir, int index)
        {
            string featureName = feat.Name;
            string featureType = feat.GetTypeName2();
            string sanitizedName = SanitizeFileName(featureName);
            string stepFolder = Path.Combine(partOutputDir, $"{index:D3}_{sanitizedName}");

            if (!Directory.Exists(stepFolder))
            {
                Directory.CreateDirectory(stepFolder);
            }

            Log($"    [{index}] {featureName} ({featureType})");

            // === STEP 1: Capture BEFORE state ===
            // Suppress the current feature to show "before" state
            feat.SetSuppression2(
                (int)swFeatureSuppressionAction_e.swSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration,
                null
            );
            swModel.EditRebuild3();
            swModel.ViewZoomtofit2();

            // Capture before screenshot
            string beforePath = Path.Combine(stepFolder, "before.png");
            CaptureScreenshot(swModel, beforePath);

            // === STEP 2: Capture AFTER state ===
            // Unsuppress the feature
            feat.SetSuppression2(
                (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration,
                null
            );
            swModel.EditRebuild3();
            swModel.ViewZoomtofit2();

            // Capture after screenshot
            string afterPath = Path.Combine(stepFolder, "after.png");
            CaptureScreenshot(swModel, afterPath);

            // === STEP 3: Capture SKETCH with dimensions ===
            string sketchPath = Path.Combine(stepFolder, "sketch.png");
            CaptureFeatureSketch(swModel, feat, sketchPath);

            // === STEP 4: Extract metadata ===
            string metadataPath = Path.Combine(stepFolder, "data.txt");
            string metadata = ExtractFeatureMetadata(feat, swModel);
            File.WriteAllText(metadataPath, metadata, Encoding.UTF8);
        }

        /// <summary>
        /// Capture the sketch associated with a feature in edit mode with dimensions visible
        /// </summary>
        private void CaptureFeatureSketch(ModelDoc2 swModel, Feature feat, string outputPath)
        {
            try
            {
                // Find the sketch associated with this feature
                Feature sketchFeature = FindAssociatedSketch(feat, swModel);
                
                if (sketchFeature == null)
                {
                    Log($"      No sketch found for this feature");
                    return;
                }

                // Select the sketch
                sketchFeature.Select2(false, 0);

                // Edit the sketch (enter sketch edit mode)
                swModel.EditSketch();

                // Wait a moment for the view to update
                System.Threading.Thread.Sleep(100);

                // Set view to normal to sketch plane (look straight at the sketch)
                swModel.ShowNamedView2("*Normal To", -1);
                
                // Zoom to fit the sketch contents
                swModel.ViewZoomtofit2();

                // Make sure dimensions are visible
                ModelDocExtension swModelExt = swModel.Extension;
                
                // Try to show all sketch dimensions
                try
                {
                    // Select all sketch entities to show their dimensions
                    swModel.Extension.SelectAll();
                    System.Threading.Thread.Sleep(50);
                }
                catch { }

                // Capture the sketch screenshot
                CaptureScreenshot(swModel, outputPath);

                // Exit sketch edit mode
                swModel.InsertSketch2(true);

                // Clear selection
                swModel.ClearSelection2(true);

                // Return to isometric view
                swModel.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                swModel.ViewZoomtofit2();

                Log($"      Sketch captured: {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex)
            {
                Log($"      Sketch capture error: {ex.Message}");
                
                // Make sure we exit sketch mode if something went wrong
                try
                {
                    swModel.InsertSketch2(true);
                    swModel.ClearSelection2(true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Find the sketch feature associated with a given feature (e.g., extrusion's profile sketch)
        /// </summary>
        private Feature FindAssociatedSketch(Feature feat, ModelDoc2 swModel = null)
        {
            try
            {
                string typeName = feat.GetTypeName2().ToLower();
                string featName = feat.Name;

                Log($"        Looking for sketch of: {featName} (type: {typeName})");

                // If this IS a sketch feature, return it directly
                if (typeName.Contains("sketch"))
                {
                    return feat;
                }

                // Method 1: Check sub-features (children) for the sketch
                Feature subFeat = feat.GetFirstSubFeature() as Feature;
                while (subFeat != null)
                {
                    string subType = subFeat.GetTypeName2();
                    Log($"          Sub-feature: {subFeat.Name} ({subType})");
                    
                    if (subType.ToLower().Contains("sketch") || subType == "ProfileFeature")
                    {
                        Log($"          Found sketch in sub-features: {subFeat.Name}");
                        return subFeat;
                    }
                    subFeat = subFeat.GetNextSubFeature() as Feature;
                }

                // Method 2: Get parents of this feature - the sketch should be a parent
                if (swModel != null)
                {
                    try
                    {
                        // Get the feature's parents
                        object[] parents = feat.GetParents() as object[];
                        if (parents != null)
                        {
                            foreach (object parentObj in parents)
                            {
                                Feature parentFeat = parentObj as Feature;
                                if (parentFeat != null)
                                {
                                    string parentType = parentFeat.GetTypeName2();
                                    Log($"          Parent: {parentFeat.Name} ({parentType})");
                                    
                                    if (parentType.ToLower().Contains("sketch") || parentType == "ProfileFeature")
                                    {
                                        Log($"          Found sketch in parents: {parentFeat.Name}");
                                        return parentFeat;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Method 3: Look for a sketch with a name that matches the feature
                // e.g., "Cut-Extrude1" might use "Sketch1" or "Sketch4"
                if (swModel != null)
                {
                    // Extract the number from the feature name (e.g., "4" from "Cut-Extrude4")
                    string featureNumber = System.Text.RegularExpressions.Regex.Match(featName, @"\d+$").Value;
                    
                    Feature treeFeat = swModel.FirstFeature() as Feature;
                    Feature bestSketch = null;
                    Feature lastSketchBeforeFeature = null;
                    bool foundOurFeature = false;
                    
                    while (treeFeat != null)
                    {
                        string treeType = treeFeat.GetTypeName2();
                        string treeName = treeFeat.Name;
                        
                        // Check if we've reached our feature
                        if (treeName == featName)
                        {
                            foundOurFeature = true;
                            // The sketch right before this feature is likely the one
                            if (lastSketchBeforeFeature != null)
                            {
                                Log($"          Found sketch before feature: {lastSketchBeforeFeature.Name}");
                                return lastSketchBeforeFeature;
                            }
                        }
                        
                        // Track sketches
                        if (treeType.ToLower().Contains("sketch") || treeType == "ProfileFeature")
                        {
                            if (!foundOurFeature)
                            {
                                lastSketchBeforeFeature = treeFeat;
                            }
                            
                            // Also check if sketch name contains same number
                            if (!string.IsNullOrEmpty(featureNumber))
                            {
                                string sketchNumber = System.Text.RegularExpressions.Regex.Match(treeName, @"\d+$").Value;
                                if (sketchNumber == featureNumber)
                                {
                                    bestSketch = treeFeat;
                                }
                            }
                        }
                        
                        treeFeat = treeFeat.GetNextFeature() as Feature;
                    }
                    
                    if (bestSketch != null)
                    {
                        Log($"          Found sketch by number match: {bestSketch.Name}");
                        return bestSketch;
                    }
                }

                Log($"          No sketch found for {featName}");
                return null;
            }
            catch (Exception ex)
            {
                Log($"          FindAssociatedSketch error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Capture a screenshot of the current model state
        /// </summary>
        private void CaptureScreenshot(ModelDoc2 swModel, string outputPath)
        {
            string bmpPath = Path.ChangeExtension(outputPath, ".bmp");

            try
            {
                // Use SaveBMP to capture the view
                bool success = swModel.SaveBMP(bmpPath, 800, 600);

                if (success && File.Exists(bmpPath))
                {
                    // Convert BMP to PNG for smaller file size
                    using (Bitmap bmp = new Bitmap(bmpPath))
                    {
                        bmp.Save(outputPath, ImageFormat.Png);
                    }

                    // Clean up BMP
                    try { File.Delete(bmpPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                Log($"      Screenshot error: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract comprehensive metadata from a feature using GetDefinition()
        /// </summary>
        private string ExtractFeatureMetadata(Feature feat, ModelDoc2 swModel)
        {
            StringBuilder sb = new StringBuilder();

            // Basic info
            sb.AppendLine("===== FEATURE METADATA =====");
            sb.AppendLine($"Name: {feat.Name}");
            sb.AppendLine($"Type: {feat.GetTypeName2()}");
            sb.AppendLine($"Suppressed: {feat.IsSuppressed()}");
            sb.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // === CRITICAL: Extract sketch plane/reference information ===
            sb.AppendLine("===== SKETCH PLANE / REFERENCE =====");
            ExtractSketchPlaneInfo(feat, swModel, sb);
            sb.AppendLine();

            // Get feature definition for parameters
            object featDef = feat.GetDefinition();
            if (featDef != null)
            {
                sb.AppendLine("===== FEATURE DEFINITION =====");
                ExtractFeatureDefinition(feat, featDef, sb);
                sb.AppendLine();
            }

            // Get associated sketch data
            sb.AppendLine("===== SKETCH DATA =====");
            ExtractSketchData(feat, sb);
            sb.AppendLine();

            // Get dimensions (all values in meters - need conversion to mm)
            sb.AppendLine("===== DIMENSIONS (Internal: Meters) =====");
            ExtractDimensions(feat, sb);
            sb.AppendLine();

            // Unit conversion note for the LLM
            sb.AppendLine("===== UNIT CONVERSION NOTES =====");
            sb.AppendLine("SolidWorks internal units are METERS.");
            sb.AppendLine("To convert to MILLIMETERS: multiply by 1000");
            sb.AppendLine("Example: 0.025 meters = 25 mm");

            return sb.ToString();
        }

        /// <summary>
        /// Extract the sketch plane or reference face information - CRITICAL for positioning
        /// </summary>
        private void ExtractSketchPlaneInfo(Feature feat, ModelDoc2 swModel, StringBuilder sb)
        {
            try
            {
                // Find the associated sketch
                Feature sketchFeature = FindAssociatedSketch(feat, swModel);
                
                if (sketchFeature != null)
                {
                    Sketch sketch = sketchFeature.GetSpecificFeature2() as Sketch;
                    if (sketch != null)
                    {
                        // Get the reference plane/face for the sketch
                        int refType = 0;
                        object refEntity = sketch.GetReferenceEntity(ref refType);
                        
                        if (refEntity != null)
                        {
                            switch (refType)
                            {
                                case 0: // Reference Plane
                                    Feature planeFeature = refEntity as Feature;
                                    if (planeFeature != null)
                                    {
                                        sb.AppendLine($"SketchPlane: {planeFeature.Name}");
                                        sb.AppendLine($"PlaneType: Reference Plane");
                                        
                                        // Check if it's a standard plane
                                        string planeName = planeFeature.Name.ToLower();
                                        if (planeName.Contains("front") || planeName.Contains("face"))
                                            sb.AppendLine("StandardPlane: Front Plane (XY)");
                                        else if (planeName.Contains("top") || planeName.Contains("dessus"))
                                            sb.AppendLine("StandardPlane: Top Plane (XZ)");
                                        else if (planeName.Contains("right") || planeName.Contains("droite"))
                                            sb.AppendLine("StandardPlane: Right Plane (YZ)");
                                        else
                                            sb.AppendLine("StandardPlane: Custom/User-defined");
                                    }
                                    break;
                                    
                                case 1: // Face
                                    Face2 face = refEntity as Face2;
                                    if (face != null)
                                    {
                                        sb.AppendLine("SketchPlane: Model Face");
                                        sb.AppendLine("PlaneType: Face of existing geometry");
                                        
                                        // Get face information
                                        Surface surface = face.GetSurface() as Surface;
                                        if (surface != null)
                                        {
                                            if (surface.IsPlane())
                                            {
                                                sb.AppendLine("FaceType: Planar");
                                                
                                                // Get plane normal to understand orientation
                                                double[] normal = face.Normal as double[];
                                                if (normal != null && normal.Length >= 3)
                                                {
                                                    sb.AppendLine($"FaceNormal: ({normal[0]:F3}, {normal[1]:F3}, {normal[2]:F3})");
                                                    
                                                    // Interpret the normal
                                                    if (Math.Abs(normal[2]) > 0.9)
                                                        sb.AppendLine("FaceOrientation: Top/Bottom face (Z-normal)");
                                                    else if (Math.Abs(normal[1]) > 0.9)
                                                        sb.AppendLine("FaceOrientation: Front/Back face (Y-normal)");
                                                    else if (Math.Abs(normal[0]) > 0.9)
                                                        sb.AppendLine("FaceOrientation: Left/Right face (X-normal)");
                                                    else
                                                        sb.AppendLine("FaceOrientation: Angled face");
                                                }
                                            }
                                            else if (surface.IsCylinder())
                                                sb.AppendLine("FaceType: Cylindrical");
                                            else
                                                sb.AppendLine("FaceType: Complex/Curved");
                                        }
                                        
                                        // Try to get the face's owning feature
                                        try
                                        {
                                            Feature ownerFeat = face.GetFeature() as Feature;
                                            if (ownerFeat != null)
                                            {
                                                sb.AppendLine($"FaceOwnerFeature: {ownerFeat.Name}");
                                            }
                                        }
                                        catch { }
                                    }
                                    break;
                                    
                                default:
                                    sb.AppendLine($"SketchPlane: Unknown reference type ({refType})");
                                    break;
                            }
                        }
                        else
                        {
                            sb.AppendLine("SketchPlane: Could not determine (may be on origin plane)");
                        }
                        
                        // Get sketch origin/transform info
                        try
                        {
                            MathTransform transform = sketch.ModelToSketchTransform;
                            if (transform != null)
                            {
                                object arrayData = transform.ArrayData;
                                if (arrayData != null)
                                {
                                    double[] transformData = arrayData as double[];
                                    if (transformData != null && transformData.Length >= 16)
                                    {
                                        // Extract translation (position) from transform matrix
                                        double tx = transformData[9] * 1000;  // Convert to mm
                                        double ty = transformData[10] * 1000;
                                        double tz = transformData[11] * 1000;
                                        
                                        if (Math.Abs(tx) > 0.001 || Math.Abs(ty) > 0.001 || Math.Abs(tz) > 0.001)
                                        {
                                            sb.AppendLine($"SketchOriginOffset: ({tx:F3}, {ty:F3}, {tz:F3}) mm");
                                        }
                                        else
                                        {
                                            sb.AppendLine("SketchOriginOffset: At origin (0, 0, 0)");
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    sb.AppendLine("No associated sketch found for this feature.");
                    
                    // Try to extract plane info from the feature name or look at what was selected
                    // Also try to find which sketch was used by looking at feature tree order
                    if (swModel != null)
                    {
                        // Look for a sketch right before this feature in the tree
                        Feature treeFeat = swModel.FirstFeature() as Feature;
                        Feature lastSketch = null;
                        
                        while (treeFeat != null)
                        {
                            if (treeFeat.Name == feat.Name)
                            {
                                // Found our feature - use the last sketch we saw
                                if (lastSketch != null)
                                {
                                    sb.AppendLine($"LikelySketch: {lastSketch.Name}");
                                    
                                    // Try to get that sketch's plane
                                    Sketch sk = lastSketch.GetSpecificFeature2() as Sketch;
                                    if (sk != null)
                                    {
                                        int rt = 0;
                                        object re = sk.GetReferenceEntity(ref rt);
                                        if (re != null && rt == 0)
                                        {
                                            Feature pf = re as Feature;
                                            if (pf != null)
                                            {
                                                sb.AppendLine($"SketchPlane: {pf.Name}");
                                            }
                                        }
                                    }
                                }
                                break;
                            }
                            
                            string treeType = treeFeat.GetTypeName2().ToLower();
                            if (treeType.Contains("sketch") || treeType == "profilefeature")
                            {
                                lastSketch = treeFeat;
                            }
                            
                            treeFeat = treeFeat.GetNextFeature() as Feature;
                        }
                    }
                    
                    // For features without sketches (like fillets, chamfers), get the selected references
                    ExtractFeatureReferences(feat, sb);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error extracting plane info: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract reference selections for features without sketches (fillets, chamfers, etc.)
        /// </summary>
        private void ExtractFeatureReferences(Feature feat, StringBuilder sb)
        {
            try
            {
                string typeName = feat.GetTypeName2().ToLower();
                
                if (typeName.Contains("fillet") || typeName.Contains("chamfer"))
                {
                    sb.AppendLine("FeatureType: Edge modification (fillet/chamfer)");
                    sb.AppendLine("AppliedTo: Selected edges of existing geometry");
                }
                else if (typeName.Contains("shell"))
                {
                    sb.AppendLine("FeatureType: Shell (hollow out)");
                    sb.AppendLine("AppliedTo: Selected faces removed, wall thickness applied");
                }
                else if (typeName.Contains("pattern"))
                {
                    sb.AppendLine("FeatureType: Pattern");
                    sb.AppendLine("AppliedTo: Selected features repeated in pattern");
                }
            }
            catch { }
        }

        /// <summary>
        /// Extract feature definition parameters based on feature type
        /// </summary>
        private void ExtractFeatureDefinition(Feature feat, object featDef, StringBuilder sb)
        {
            string typeName = feat.GetTypeName2().ToLower();

            try
            {
                // Handle extrusion features (including "ICE" which is internal code for Cut-Extrude)
                if (typeName.Contains("extrude") || typeName.Contains("extrusion") || typeName == "ice")
                {
                    ExtrudeFeatureData2 extDef = featDef as ExtrudeFeatureData2;
                    if (extDef != null)
                    {
                        sb.AppendLine($"Direction1:");
                        sb.AppendLine($"  EndCondition: {GetEndConditionName(extDef.GetEndCondition(true))}");
                        sb.AppendLine($"  Depth: {extDef.GetDepth(true):F6} m ({extDef.GetDepth(true) * 1000:F3} mm)");
                        sb.AppendLine($"  DraftAngle: {extDef.GetDraftAngle(true) * 180 / Math.PI:F2}°");
                        
                        if (extDef.BothDirections)
                        {
                            sb.AppendLine($"Direction2:");
                            sb.AppendLine($"  EndCondition: {GetEndConditionName(extDef.GetEndCondition(false))}");
                            sb.AppendLine($"  Depth: {extDef.GetDepth(false):F6} m ({extDef.GetDepth(false) * 1000:F3} mm)");
                        }
                        
                        // Use method calls properly
                        try
                        {
                            sb.AppendLine($"IsBossFeature: {extDef.IsBossFeature()}");
                            sb.AppendLine($"IsThinFeature: {extDef.IsThinFeature()}");
                        }
                        catch
                        {
                            // Some versions may not have these methods
                        }
                    }
                }
                // Handle revolve features
                else if (typeName.Contains("revolve") || typeName.Contains("revolution"))
                {
                    RevolveFeatureData2 revDef = featDef as RevolveFeatureData2;
                    if (revDef != null)
                    {
                        try
                        {
                            // GetRevolutionAngle requires a boolean parameter (true = direction 1)
                            double angle = revDef.GetRevolutionAngle(true);
                            sb.AppendLine($"Angle: {angle * 180 / Math.PI:F2}°");
                            sb.AppendLine($"IsBossFeature: {revDef.IsBossFeature()}");
                            sb.AppendLine($"IsThinFeature: {revDef.IsThinFeature()}");
                        }
                        catch
                        {
                            sb.AppendLine("Could not extract revolve parameters");
                        }
                    }
                }
                // Handle fillet features
                else if (typeName.Contains("fillet"))
                {
                    ISimpleFilletFeatureData2 filletData = featDef as ISimpleFilletFeatureData2;
                    if (filletData != null)
                    {
                        try
                        {
                            sb.AppendLine($"Radius: {filletData.DefaultRadius:F6} m ({filletData.DefaultRadius * 1000:F3} mm)");
                            sb.AppendLine($"Type: {filletData.Type}");
                        }
                        catch
                        {
                            sb.AppendLine("Could not extract fillet parameters");
                        }
                    }
                }
                // Handle chamfer features  
                else if (typeName.Contains("chamfer"))
                {
                    // Chamfer feature data - extract what we can from dimensions
                    sb.AppendLine("ChamferFeature detected - dimensions extracted separately");
                }
                // Handle hole wizard
                else if (typeName.Contains("hole"))
                {
                    WizardHoleFeatureData2 holeWzd = featDef as WizardHoleFeatureData2;
                    if (holeWzd != null)
                    {
                        try
                        {
                            sb.AppendLine($"HoleType: {holeWzd.Type}");
                            sb.AppendLine($"Standard: {holeWzd.Standard}");
                            sb.AppendLine($"FastenerType: {holeWzd.FastenerType}");
                            sb.AppendLine($"HoleDepth: {holeWzd.HoleDepth * 1000:F3} mm");
                            sb.AppendLine($"EndCondition: {holeWzd.EndCondition}");
                        }
                        catch
                        {
                            sb.AppendLine("Could not extract hole wizard parameters");
                        }
                    }
                }
                // Handle shell
                else if (typeName.Contains("shell"))
                {
                    IShellFeatureData shellDef = featDef as IShellFeatureData;
                    if (shellDef != null)
                    {
                        try
                        {
                            sb.AppendLine($"Thickness: {shellDef.Thickness * 1000:F3} mm");
                        }
                        catch
                        {
                            sb.AppendLine("Could not extract shell parameters");
                        }
                    }
                }
                // Handle patterns
                else if (typeName.Contains("pattern") || typeName == "lpattern" || typeName == "cpattern")
                {
                    ILinearPatternFeatureData lpDef = featDef as ILinearPatternFeatureData;
                    if (lpDef != null)
                    {
                        try
                        {
                            sb.AppendLine($"Direction1:");
                            sb.AppendLine($"  InstanceCount: {lpDef.D1TotalInstances}");
                            sb.AppendLine($"  Spacing: {lpDef.D1Spacing * 1000:F3} mm");
                            sb.AppendLine($"Direction2:");
                            sb.AppendLine($"  InstanceCount: {lpDef.D2TotalInstances}");
                            sb.AppendLine($"  Spacing: {lpDef.D2Spacing * 1000:F3} mm");
                        }
                        catch
                        {
                            sb.AppendLine("Could not extract linear pattern parameters");
                        }
                    }

                    ICircularPatternFeatureData cpDef = featDef as ICircularPatternFeatureData;
                    if (cpDef != null)
                    {
                        try
                        {
                            sb.AppendLine($"TotalInstances: {cpDef.TotalInstances}");
                            sb.AppendLine($"Spacing: {cpDef.Spacing * 180 / Math.PI:F2}°");
                            sb.AppendLine($"EqualSpacing: {cpDef.EqualSpacing}");
                        }
                        catch
                        {
                            sb.AppendLine("Could not extract circular pattern parameters");
                        }
                    }
                }
                else
                {
                    // Generic - just note the type
                    sb.AppendLine($"FeatureDefinitionType: {featDef.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error extracting definition: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract sketch geometry data - critical for spatial positioning
        /// </summary>
        private void ExtractSketchData(Feature feat, StringBuilder sb)
        {
            try
            {
                // Get the sketch associated with this feature
                object specificFeat = feat.GetSpecificFeature2();

                // Try to get sketch from the feature
                Sketch sketch = null;

                // For sketch features directly
                if (feat.GetTypeName2().ToLower().Contains("sketch"))
                {
                    sketch = specificFeat as Sketch;
                }
                else
                {
                    // For extrusion/cut features, try to access through sub-features
                    try
                    {
                        // Try to get sub-features which might include the sketch
                        Feature subFeat = feat.GetFirstSubFeature() as Feature;
                        while (subFeat != null)
                        {
                            if (subFeat.GetTypeName2().ToLower().Contains("sketch"))
                            {
                                sketch = subFeat.GetSpecificFeature2() as Sketch;
                                break;
                            }
                            subFeat = subFeat.GetNextSubFeature() as Feature;
                        }
                    }
                    catch { }
                }

                if (sketch != null)
                {
                    ExtractSketchGeometry(sketch, sb);
                }
                else
                {
                    sb.AppendLine("No direct sketch access available for this feature type.");
                    sb.AppendLine("Check the feature's sub-features for sketch data.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error extracting sketch: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract detailed sketch geometry (lines, arcs, circles)
        /// </summary>
        private void ExtractSketchGeometry(Sketch sketch, StringBuilder sb)
        {
            if (sketch == null)
            {
                sb.AppendLine("Sketch is null");
                return;
            }

            try
            {
                // Get sketch segments
                object[] segments = sketch.GetSketchSegments() as object[];
                if (segments == null || segments.Length == 0)
                {
                    sb.AppendLine("No sketch segments found");
                    return;
                }

                sb.AppendLine($"Total Segments: {segments.Length}");
                sb.AppendLine();

                int lineCount = 0, arcCount = 0, circleCount = 0, otherCount = 0;

                foreach (object segObj in segments)
                {
                    SketchSegment seg = segObj as SketchSegment;
                    if (seg == null) continue;
                    
                    // Try to identify the segment type by casting
                    SketchLine line = seg as SketchLine;
                    if (line != null)
                    {
                        lineCount++;
                        try
                        {
                            SketchPoint startPt = line.GetStartPoint2() as SketchPoint;
                            SketchPoint endPt = line.GetEndPoint2() as SketchPoint;
                            if (startPt != null && endPt != null)
                            {
                                double length = Math.Sqrt(
                                    Math.Pow(endPt.X - startPt.X, 2) +
                                    Math.Pow(endPt.Y - startPt.Y, 2)
                                );
                                sb.AppendLine($"Line {lineCount}:");
                                sb.AppendLine($"  Start: ({startPt.X * 1000:F3}, {startPt.Y * 1000:F3}) mm");
                                sb.AppendLine($"  End: ({endPt.X * 1000:F3}, {endPt.Y * 1000:F3}) mm");
                                sb.AppendLine($"  Length: {length * 1000:F3} mm");
                            }
                        }
                        catch { }
                        continue;
                    }

                    SketchArc arc = seg as SketchArc;
                    if (arc != null)
                    {
                        arcCount++;
                        try
                        {
                            SketchPoint center = arc.GetCenterPoint2() as SketchPoint;
                            if (center != null)
                            {
                                double radius = arc.GetRadius();
                                
                                // Get start and end points to calculate arc span
                                SketchPoint startPt = arc.GetStartPoint2() as SketchPoint;
                                SketchPoint endPt = arc.GetEndPoint2() as SketchPoint;
                                
                                sb.AppendLine($"Arc/Circle {arcCount}:");
                                sb.AppendLine($"  Center: ({center.X * 1000:F3}, {center.Y * 1000:F3}) mm");
                                sb.AppendLine($"  Radius: {radius * 1000:F3} mm");
                                sb.AppendLine($"  Diameter: {radius * 2000:F3} mm");
                                
                                if (startPt != null && endPt != null)
                                {
                                    // Check if it's a full circle (start and end points are the same)
                                    double distStartEnd = Math.Sqrt(
                                        Math.Pow(endPt.X - startPt.X, 2) +
                                        Math.Pow(endPt.Y - startPt.Y, 2)
                                    );
                                    if (distStartEnd < 0.0001) // Very close = full circle
                                    {
                                        circleCount++;
                                        sb.AppendLine($"  Type: Full Circle");
                                    }
                                    else
                                    {
                                        sb.AppendLine($"  StartPoint: ({startPt.X * 1000:F3}, {startPt.Y * 1000:F3}) mm");
                                        sb.AppendLine($"  EndPoint: ({endPt.X * 1000:F3}, {endPt.Y * 1000:F3}) mm");
                                    }
                                }
                            }
                        }
                        catch { }
                        continue;
                    }

                    // Other segment types
                    otherCount++;
                }

                sb.AppendLine();
                sb.AppendLine($"Geometry Summary: {lineCount} lines, {arcCount} arcs/circles, {otherCount} other");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error extracting geometry: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract all dimensions from a feature
        /// </summary>
        private void ExtractDimensions(Feature feat, StringBuilder sb)
        {
            try
            {
                DisplayDimension dispDim = feat.GetFirstDisplayDimension() as DisplayDimension;
                int dimCount = 0;

                while (dispDim != null)
                {
                    dimCount++;
                    Dimension dim = dispDim.GetDimension2(0) as Dimension;
                    
                    if (dim != null)
                    {
                        sb.AppendLine($"Dimension {dimCount}:");
                        sb.AppendLine($"  Name: {dim.FullName}");
                        sb.AppendLine($"  Value: {dim.SystemValue:F6} m ({dim.SystemValue * 1000:F3} mm)");
                    }

                    dispDim = dispDim.GetNext5() as DisplayDimension;
                }

                if (dimCount == 0)
                {
                    sb.AppendLine("No explicit dimensions found.");
                    sb.AppendLine("Dimensions may be embedded in feature definition.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error extracting dimensions: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert end condition enum to readable name
        /// </summary>
        private string GetEndConditionName(int endCondition)
        {
            switch ((swEndConditions_e)endCondition)
            {
                case swEndConditions_e.swEndCondBlind: return "Blind";
                case swEndConditions_e.swEndCondThroughAll: return "Through All";
                case swEndConditions_e.swEndCondThroughAllBoth: return "Through All Both";
                case swEndConditions_e.swEndCondUpToVertex: return "Up To Vertex";
                case swEndConditions_e.swEndCondUpToSurface: return "Up To Surface";
                case swEndConditions_e.swEndCondOffsetFromSurface: return "Offset From Surface";
                case swEndConditions_e.swEndCondMidPlane: return "Mid Plane";
                case swEndConditions_e.swEndCondUpToBody: return "Up To Body";
                case swEndConditions_e.swEndCondUpToNext: return "Up To Next";
                default: return $"Unknown ({endCondition})";
            }
        }

        /// <summary>
        /// Sanitize a file name by removing invalid characters
        /// </summary>
        private string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            // Also replace spaces and special chars
            name = name.Replace(' ', '_').Replace('<', '_').Replace('>', '_');
            return name;
        }

        /// <summary>
        /// Log a message
        /// </summary>
        private void Log(string message)
        {
            logBuilder.AppendLine(message);
            System.Diagnostics.Debug.WriteLine($"[FeatureWalker] {message}");
        }
    }
}
