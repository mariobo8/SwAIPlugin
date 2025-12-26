using System;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwAIPlugin
{
    /// <summary>
    /// Analyzes SolidWorks models and extracts information for AI context
    /// Includes screenshot capture for vision models
    /// </summary>
    public class ModelAnalyzer
    {
        private SldWorks swApp;

        public ModelAnalyzer(SldWorks app)
        {
            swApp = app;
        }

        /// <summary>
        /// Captures a screenshot of the current model and returns it as base64 PNG
        /// </summary>
        public string CaptureModelScreenshot()
        {
            string bmpPath = null;
            string pngPath = null;
            
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                if (swModel == null)
                {
                    return null;
                }

                // Get the model view
                ModelView modelView = swModel.ActiveView as ModelView;
                if (modelView == null)
                {
                    return null;
                }

                // Zoom to fit first
                swModel.ViewZoomtofit2();

                // Create temporary file paths
                string tempId = Guid.NewGuid().ToString();
                bmpPath = Path.Combine(Path.GetTempPath(), $"sw_screenshot_{tempId}.bmp");
                pngPath = Path.Combine(Path.GetTempPath(), $"sw_screenshot_{tempId}.png");

                // Use SaveBMP to capture the view (SolidWorks always saves as BMP)
                bool success = swModel.SaveBMP(bmpPath, 800, 600);
                
                if (success && File.Exists(bmpPath))
                {
                    // Load the BMP and convert to PNG for API compatibility
                    using (Bitmap bmp = new Bitmap(bmpPath))
                    {
                        // Save as PNG format
                        bmp.Save(pngPath, ImageFormat.Png);
                    }
                    
                    if (File.Exists(pngPath))
                    {
                        // Read the PNG and convert to base64
                        byte[] imageBytes = File.ReadAllBytes(pngPath);
                        string base64 = Convert.ToBase64String(imageBytes);
                        return base64;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screenshot capture error: {ex.Message}");
                return null;
            }
            finally
            {
                // Clean up temp files
                try
                {
                    if (bmpPath != null && File.Exists(bmpPath))
                        File.Delete(bmpPath);
                    if (pngPath != null && File.Exists(pngPath))
                        File.Delete(pngPath);
                }
                catch { }
            }
        }

        /// <summary>
        /// Analyzes the active model and returns a detailed description
        /// </summary>
        public string AnalyzeModel()
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                
                if (swModel == null)
                {
                    return "No active model. Please open a part or assembly.";
                }

                StringBuilder analysis = new StringBuilder();
                analysis.AppendLine("=== SOLIDWORKS MODEL ANALYSIS ===");
                analysis.AppendLine();

                // Basic Model Information
                analysis.AppendLine("MODEL TYPE: " + GetModelType(swModel));
                analysis.AppendLine("FILE NAME: " + swModel.GetTitle());
                
                string filePath = swModel.GetPathName();
                if (!string.IsNullOrEmpty(filePath))
                {
                    analysis.AppendLine("FILE PATH: " + filePath);
                }
                analysis.AppendLine();

                // Part-specific analysis
                if (swModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                {
                    PartDoc partDoc = swModel as PartDoc;
                    analysis.AppendLine(AnalyzePart(partDoc, swModel));
                    
                    // Feature Tree with details
                    analysis.AppendLine();
                    analysis.AppendLine("FEATURES:");
                    analysis.AppendLine(GetDetailedFeatureTree(swModel));

                    // Bounding Box / Dimensions
                    analysis.AppendLine();
                    analysis.AppendLine("BOUNDING BOX:");
                    analysis.AppendLine(GetBoundingBox(swModel));

                    // Mass Properties (if available)
                    analysis.AppendLine();
                    analysis.AppendLine("MASS PROPERTIES:");
                    analysis.AppendLine(GetMassProperties(swModel));
                }
                // Assembly-specific analysis
                else if (swModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    AssemblyDoc assyDoc = swModel as AssemblyDoc;
                    analysis.AppendLine(AnalyzeAssembly(assyDoc, swModel));
                    
                    // Feature Tree
                    analysis.AppendLine();
                    analysis.AppendLine("FEATURES:");
                    analysis.AppendLine(GetDetailedFeatureTree(swModel));
                }
                // Drawing-specific analysis
                else if (swModel.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    DrawingDoc drawDoc = swModel as DrawingDoc;
                    analysis.AppendLine(AnalyzeDrawing(drawDoc, swModel));
                }

                return analysis.ToString();
            }
            catch (Exception ex)
            {
                return $"Error analyzing model: {ex.Message}";
            }
        }

        private string GetModelType(ModelDoc2 swModel)
        {
            int docType = swModel.GetType();
            switch (docType)
            {
                case (int)swDocumentTypes_e.swDocPART:
                    return "PART";
                case (int)swDocumentTypes_e.swDocASSEMBLY:
                    return "ASSEMBLY";
                case (int)swDocumentTypes_e.swDocDRAWING:
                    return "DRAWING";
                default:
                    return "UNKNOWN";
            }
        }

        private string AnalyzePart(PartDoc partDoc, ModelDoc2 swModel)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("PART ANALYSIS:");
            
            try
            {
                // Get feature count
                FeatureManager featureMgr = swModel.FeatureManager;
                int featureCount = featureMgr.GetFeatureCount(false);
                info.AppendLine($"  Total Features: {featureCount}");

                // Get body count
                object bodiesObj = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, false);
                if (bodiesObj != null)
                {
                    object[] bodies = bodiesObj as object[];
                    if (bodies != null)
                {
                    info.AppendLine($"  Solid Bodies: {bodies.Length}");
                        
                        // Analyze each body
                        for (int i = 0; i < bodies.Length && i < 5; i++)
                        {
                            Body2 body = bodies[i] as Body2;
                            if (body != null)
                            {
                                info.AppendLine($"    Body {i + 1}: {body.Name}");
                                
                                // Get face count
                                object[] faces = body.GetFaces() as object[];
                                if (faces != null)
                                {
                                    info.AppendLine($"      Faces: {faces.Length}");
                                }
                                
                                // Get edge count
                                object[] edges = body.GetEdges() as object[];
                                if (edges != null)
                                {
                                    info.AppendLine($"      Edges: {edges.Length}");
                                }
                            }
                        }
                    }
                }

                // Get surface body count
                object surfaceBodiesObj = partDoc.GetBodies2((int)swBodyType_e.swSheetBody, false);
                if (surfaceBodiesObj != null)
                {
                    object[] surfaceBodies = surfaceBodiesObj as object[];
                    if (surfaceBodies != null && surfaceBodies.Length > 0)
                {
                    info.AppendLine($"  Surface Bodies: {surfaceBodies.Length}");
                    }
                }

                // Check configuration
                ConfigurationManager configMgr = swModel.ConfigurationManager;
                Configuration activeConfig = configMgr.ActiveConfiguration;
                if (activeConfig != null)
                {
                    info.AppendLine($"  Active Configuration: {activeConfig.Name}");
                    
                    // Get all configurations
                    string[] configNames = swModel.GetConfigurationNames() as string[];
                    if (configNames != null && configNames.Length > 1)
                    {
                        info.AppendLine($"  Total Configurations: {configNames.Length}");
                    }
                }

                // Material info
                string material = partDoc.GetMaterialPropertyName2("", out string database);
                if (!string.IsNullOrEmpty(material))
                {
                    info.AppendLine($"  Material: {material}");
                }
            }
            catch (Exception ex)
            {
                info.AppendLine($"  Error: {ex.Message}");
            }

            return info.ToString();
        }

        private string AnalyzeAssembly(AssemblyDoc assyDoc, ModelDoc2 swModel)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("ASSEMBLY ANALYSIS:");
            
            try
            {
                // Get component count
                object componentsObj = assyDoc.GetComponents(false);
                if (componentsObj != null)
                {
                    object[] components = componentsObj as object[];
                    if (components != null)
                {
                    info.AppendLine($"  Total Components: {components.Length}");
                        
                        // List first few components
                        for (int i = 0; i < components.Length && i < 10; i++)
                        {
                            Component2 comp = components[i] as Component2;
                            if (comp != null)
                            {
                                string suppressed = comp.IsSuppressed() ? " (Suppressed)" : "";
                                info.AppendLine($"    - {comp.Name2}{suppressed}");
                            }
                        }
                        
                        if (components.Length > 10)
                        {
                            info.AppendLine($"    ... and {components.Length - 10} more components");
                        }
                    }
                }

                // Get mate count
                FeatureManager featureMgr = swModel.FeatureManager;
                int featureCount = featureMgr.GetFeatureCount(false);
                info.AppendLine($"  Assembly Features: {featureCount}");
            }
            catch (Exception ex)
            {
                info.AppendLine($"  Error: {ex.Message}");
            }

            return info.ToString();
        }

        private string AnalyzeDrawing(DrawingDoc drawDoc, ModelDoc2 swModel)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("=== DRAWING ANALYSIS ===");
            info.AppendLine();
            info.AppendLine("Please review this drawing for the following potential issues:");
            info.AppendLine();
            
            try
            {
                // Get all sheets
                object[] sheetNames = drawDoc.GetSheetNames() as object[];
                int totalSheets = sheetNames != null ? sheetNames.Length : 0;
                info.AppendLine($"SHEETS: {totalSheets}");
                
                int totalViews = 0;
                int totalDimensions = 0;
                int totalAnnotations = 0;
                bool hasDetailView = false;
                bool hasSectionView = false;
                bool hasIsometricView = false;
                
                // Analyze each sheet
                if (sheetNames != null)
                {
                    foreach (string sheetName in sheetNames)
                    {
                        Sheet sheet = drawDoc.Sheet[sheetName] as Sheet;
                        if (sheet == null) continue;
                        
                        info.AppendLine();
                        info.AppendLine($"  Sheet: {sheetName}");
                        
                        // Get sheet properties
                        double width = 0, height = 0;
                        sheet.GetSize(ref width, ref height);
                        info.AppendLine($"    Size: {width * 1000:F0} x {height * 1000:F0} mm");
                        
                        // Get scale
                        double scale = sheet.GetProperties2()[2];
                        info.AppendLine($"    Scale: 1:{1/scale:F0}");
                        
                        // Get views on this sheet
                        object[] views = drawDoc.GetViews()[0] as object[];
                        if (views != null)
                        {
                            int sheetViewCount = 0;
                            foreach (View view in views)
                            {
                                if (view == null) continue;
                                sheetViewCount++;
                                totalViews++;
                                
                                string viewType = view.Type.ToString();
                                string viewName = view.Name;
                                
                                // Check view types
                                if (viewType.Contains("Detail")) hasDetailView = true;
                                if (viewType.Contains("Section")) hasSectionView = true;
                                if (viewName.ToLower().Contains("isometric") || viewName.ToLower().Contains("iso"))
                                    hasIsometricView = true;
                                
                                // Count dimensions in this view
                                DisplayDimension dispDim = view.GetFirstDisplayDimension5() as DisplayDimension;
                                int viewDimCount = 0;
                                while (dispDim != null)
                                {
                                    viewDimCount++;
                                    totalDimensions++;
                                    dispDim = dispDim.GetNext5() as DisplayDimension;
                                }
                                
                                // Count annotations
                                object[] annots = view.GetAnnotations() as object[];
                                if (annots != null)
                                {
                                    totalAnnotations += annots.Length;
                                }
                            }
                            info.AppendLine($"    Views: {sheetViewCount}");
                        }
                    }
                }
                
                info.AppendLine();
                info.AppendLine("SUMMARY:");
                info.AppendLine($"  Total Views: {totalViews}");
                info.AppendLine($"  Total Dimensions: {totalDimensions}");
                info.AppendLine($"  Total Annotations: {totalAnnotations}");
                info.AppendLine($"  Has Detail View: {(hasDetailView ? "Yes" : "No")}");
                info.AppendLine($"  Has Section View: {(hasSectionView ? "Yes" : "No")}");
                info.AppendLine($"  Has Isometric View: {(hasIsometricView ? "Yes" : "No")}");
                
                // Potential issues to check
                info.AppendLine();
                info.AppendLine("ITEMS TO REVIEW:");
                
                if (totalDimensions == 0)
                {
                    info.AppendLine("  ⚠ WARNING: No dimensions found - drawing may be incomplete");
                }
                else if (totalDimensions < 5)
                {
                    info.AppendLine("  ⚠ NOTE: Very few dimensions - verify all critical dimensions are present");
                }
                
                if (totalViews == 0)
                {
                    info.AppendLine("  ⚠ WARNING: No views found - drawing appears empty");
                }
                else if (totalViews == 1)
                {
                    info.AppendLine("  ⚠ NOTE: Only one view - consider adding additional views for clarity");
                }
                
                if (!hasIsometricView)
                {
                    info.AppendLine("  ℹ SUGGESTION: Consider adding an isometric view for better visualization");
                }
                
                if (!hasDetailView && !hasSectionView)
                {
                    info.AppendLine("  ℹ SUGGESTION: Consider adding detail or section views for complex features");
                }
                
                info.AppendLine();
                info.AppendLine("AI REVIEW REQUEST:");
                info.AppendLine("Please analyze the attached screenshot and check for:");
                info.AppendLine("  1. Missing dimensions (especially critical ones)");
                info.AppendLine("  2. Proper view arrangement and alignment");
                info.AppendLine("  3. Correct scale usage");
                info.AppendLine("  4. Title block completeness");
                info.AppendLine("  5. Proper tolerancing and GD&T symbols");
                info.AppendLine("  6. Clear leader lines and annotations");
                info.AppendLine("  7. Hidden lines visibility settings");
                info.AppendLine("  8. Overall drawing clarity and readability");
            }
            catch (Exception ex)
            {
                info.AppendLine($"  Error analyzing drawing: {ex.Message}");
                info.AppendLine();
                info.AppendLine("Please analyze the screenshot visually for drawing quality issues.");
            }

            return info.ToString();
        }

        private string GetDetailedFeatureTree(ModelDoc2 swModel)
        {
            StringBuilder features = new StringBuilder();
            
            try
            {
                Feature feat = swModel.FirstFeature() as Feature;
                int count = 0;
                int maxFeatures = 30; // Limit to avoid overwhelming the AI

                while (feat != null && count < maxFeatures)
                {
                    string typeName = feat.GetTypeName2();
                    string name = feat.Name;
                    
                    // Skip system features that aren't user-created
                    if (!IsSystemFeature(typeName))
                    {
                        bool suppressed = feat.IsSuppressed();
                        string status = suppressed ? " [Suppressed]" : "";
                        
                        features.AppendLine($"  {name} ({typeName}){status}");
                        
                        // Get feature-specific info
                        string featureDetails = GetFeatureDetails(feat, typeName);
                        if (!string.IsNullOrEmpty(featureDetails))
                        {
                            features.AppendLine($"    {featureDetails}");
                        }
                        
                        count++;
                    }

                    feat = feat.GetNextFeature() as Feature;
                }

                if (count == 0)
                {
                    features.AppendLine("  No user-created features found.");
                }
                else if (count >= maxFeatures)
                {
                    features.AppendLine($"  ... (showing first {maxFeatures} features)");
                }
            }
            catch (Exception ex)
            {
                features.AppendLine($"  Error reading features: {ex.Message}");
            }

            return features.ToString();
        }

        private bool IsSystemFeature(string typeName)
        {
            // List of system/default features to skip
            string[] systemTypes = {
                "OriginProfileFeature",
                "RefPlane", // Only skip the default planes
                "MaterialFolder",
                "SensorFolder",
                "RefAxis",
                "DetailCabinet",
                "HistoryFolder",
                "CommentsFolder",
                "FavoriteFolder"
            };

            foreach (string sysType in systemTypes)
            {
                if (typeName.Equals(sysType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetFeatureDetails(Feature feat, string typeName)
        {
            try
            {
                switch (typeName.ToLower())
                {
                    case "extrusion":
                    case "boss-extrude":
                    case "cut-extrude":
                        return "Extrusion feature";
                    
                    case "revolution":
                    case "revolve":
                        return "Revolved feature";
                    
                    case "hole":
                    case "holewizard":
                        return "Hole feature";
                    
                    case "fillet":
                        return "Fillet/Round";
                    
                    case "chamfer":
                        return "Chamfer";
                    
                    case "shell":
                        return "Shell feature";
                    
                    case "lpattern":
                    case "linearpattern":
                        return "Linear Pattern";
                    
                    case "cpattern":
                    case "circularpattern":
                        return "Circular Pattern";
                    
                    case "mirror":
                    case "mirrorpattern":
                        return "Mirror feature";
                    
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private string GetBoundingBox(ModelDoc2 swModel)
        {
            StringBuilder bbox = new StringBuilder();
            
            try
            {
                if (swModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                {
                    PartDoc partDoc = swModel as PartDoc;
                    
                    // Get the first solid body
                    object bodiesObj = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, false);
                    if (bodiesObj != null)
                    {
                        object[] bodies = bodiesObj as object[];
                        if (bodies != null && bodies.Length > 0)
                        {
                            Body2 body = bodies[0] as Body2;
                            if (body != null)
                            {
                                // Get bounding box
                                object boxObj = body.GetBodyBox();
                                if (boxObj != null)
                                {
                                    double[] box = boxObj as double[];
                                    if (box != null && box.Length >= 6)
                                    {
                                        // Convert from meters to mm
                                        double xMin = box[0] * 1000;
                                        double yMin = box[1] * 1000;
                                        double zMin = box[2] * 1000;
                                        double xMax = box[3] * 1000;
                                        double yMax = box[4] * 1000;
                                        double zMax = box[5] * 1000;

                                        double width = Math.Abs(xMax - xMin);
                                        double height = Math.Abs(yMax - yMin);
                                        double depth = Math.Abs(zMax - zMin);

                                        bbox.AppendLine($"  Width (X): {width:F2} mm");
                                        bbox.AppendLine($"  Height (Y): {height:F2} mm");
                                        bbox.AppendLine($"  Depth (Z): {depth:F2} mm");
                                        bbox.AppendLine($"  Overall Size: {width:F1} x {height:F1} x {depth:F1} mm");
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (bbox.Length == 0)
                {
                    bbox.AppendLine("  Bounding box not available");
                }
            }
            catch (Exception ex)
            {
                bbox.AppendLine($"  Error: {ex.Message}");
            }

            return bbox.ToString();
        }

        private string GetMassProperties(ModelDoc2 swModel)
        {
            StringBuilder props = new StringBuilder();
            
            try
            {
                // Get mass properties
                int status = 0;
                object massPropsObj = swModel.Extension.GetMassProperties(1, ref status);
                
                if (massPropsObj != null && status == 0)
                {
                    double[] massProps = massPropsObj as double[];
                    if (massProps != null && massProps.Length >= 5)
                    {
                        // Mass properties array:
                        // [0-2]: Center of mass (X, Y, Z)
                        // [3]: Volume
                        // [4]: Surface area
                        // [5]: Mass
                        // [6-11]: Moments of inertia
                        
                        if (massProps.Length > 5)
                        {
                            double mass = massProps[5];
                            props.AppendLine($"  Mass: {mass * 1000:F2} grams ({mass:F4} kg)");
                        }
                        
                        double volume = massProps[3];
                        props.AppendLine($"  Volume: {volume * 1e9:F2} mm³ ({volume * 1e6:F4} cm³)");
                        
                        double surfaceArea = massProps[4];
                        props.AppendLine($"  Surface Area: {surfaceArea * 1e6:F2} mm²");
                        
                        // Center of mass
                        double comX = massProps[0] * 1000;
                        double comY = massProps[1] * 1000;
                        double comZ = massProps[2] * 1000;
                        props.AppendLine($"  Center of Mass: ({comX:F2}, {comY:F2}, {comZ:F2}) mm");
                    }
                }
                else
                {
                    props.AppendLine("  Mass properties not available (ensure material is assigned)");
                }
            }
            catch (Exception ex)
            {
                props.AppendLine($"  Error: {ex.Message}");
            }
            
            return props.ToString();
        }

        /// <summary>
        /// Gets a simplified JSON representation of the model for AI context
        /// </summary>
        public string GetModelContextJSON()
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                
                if (swModel == null)
                {
                    return "{\"has_model\": false, \"message\": \"No active model\"}";
                }

                StringBuilder json = new StringBuilder();
                json.Append("{");
                json.Append($"\"has_model\": true,");
                json.Append($"\"model_type\": \"{GetModelType(swModel)}\",");
                json.Append($"\"file_name\": \"{EscapeJson(swModel.GetTitle())}\",");

                if (swModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                {
                    PartDoc partDoc = swModel as PartDoc;
                    FeatureManager featureMgr = swModel.FeatureManager;
                    int featureCount = featureMgr.GetFeatureCount(false);
                    
                    object bodiesObj = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, false);
                    int bodyCount = 0;
                    if (bodiesObj != null)
                    {
                        object[] bodies = bodiesObj as object[];
                        bodyCount = bodies != null ? bodies.Length : 0;
                    }
                    
                    json.Append($"\"feature_count\": {featureCount},");
                    json.Append($"\"solid_bodies\": {bodyCount},");
                }

                json.Append($"\"analysis\": \"{EscapeJson(AnalyzeModel())}\"");
                json.Append("}");

                return json.ToString();
            }
            catch (Exception ex)
            {
                return $"{{\"error\": \"{EscapeJson(ex.Message)}\"}}";
            }
        }

        /// <summary>
        /// Gets a list of all face names/indices that can be selected for operations
        /// </summary>
        public string GetSelectableFaces()
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                if (swModel == null || swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    return "No part document open";
                }

                PartDoc partDoc = swModel as PartDoc;
                StringBuilder faces = new StringBuilder();
                faces.AppendLine("SELECTABLE FACES:");

                object bodiesObj = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, false);
                if (bodiesObj != null)
                {
                    object[] bodies = bodiesObj as object[];
                    if (bodies != null)
                    {
                        foreach (Body2 body in bodies)
                        {
                            object[] faceObjs = body.GetFaces() as object[];
                            if (faceObjs != null)
                            {
                                int faceIndex = 0;
                                foreach (Face2 face in faceObjs)
                                {
                                    Surface surface = face.GetSurface() as Surface;
                                    string surfaceType = GetSurfaceType(surface);
                                    faces.AppendLine($"  Face {faceIndex}: {surfaceType}");
                                    faceIndex++;
                                }
                            }
                        }
                    }
                }

                return faces.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting faces: {ex.Message}";
            }
        }

        private string GetSurfaceType(Surface surface)
        {
            if (surface == null) return "Unknown";
            
            if (surface.IsPlane()) return "Planar";
            if (surface.IsCylinder()) return "Cylindrical";
            if (surface.IsCone()) return "Conical";
            if (surface.IsSphere()) return "Spherical";
            if (surface.IsTorus()) return "Toroidal";
            
            return "Complex";
        }

        private string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
