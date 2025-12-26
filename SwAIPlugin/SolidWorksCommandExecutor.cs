using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwAIPlugin
{
    /// <summary>
    /// Executes SolidWorks commands parsed from AI responses
    /// Supports creating parts, features, holes, and modifications
    /// </summary>
    public class SolidWorksCommandExecutor
    {
        private SldWorks swApp;

        public SolidWorksCommandExecutor(SldWorks app)
        {
            swApp = app;
        }

        #region Main Execution Methods

        /// <summary>
        /// Executes a command based on parsed JSON structure
        /// </summary>
        public string ExecuteCommand(string action, string type, Dictionary<string, object> parameters)
        {
            try
            {
                action = action?.ToLower() ?? "";
                type = type?.ToLower() ?? "";

                switch (action)
                {
                    case "create_part":
                    case "new_part":
                        return CreateNewPart(type, parameters);

                    case "create_feature":
                    case "create":
                    case "add":
                        return CreateFeature(type, parameters);

                    case "modify":
                    case "modify_feature":
                    case "edit":
                        return ModifyFeature(type, parameters);

                    case "delete":
                    case "remove":
                        return DeleteFeature(parameters);
                    
                    default:
                        return $"Unknown action '{action}'. Supported: create_part, create_feature, create, add, modify, delete";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}\n{ex.StackTrace}";
            }
        }

        /// <summary>
        /// Creates a new part document
        /// </summary>
        public string CreateNewPart(string type, Dictionary<string, object> parameters)
        {
            try
            {
                // Skip geometry if type is empty or "empty"
                bool createEmpty = string.IsNullOrEmpty(type) || type.ToLower() == "empty" || type.ToLower() == "none";
                
                ModelDoc2 newPart = null;
                string usedTemplate = "";
                
                // Method 1: Try user's default template setting
                string defaultTemplate = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                if (!string.IsNullOrEmpty(defaultTemplate) && System.IO.File.Exists(defaultTemplate))
                {
                    newPart = swApp.NewDocument(defaultTemplate, 0, 0, 0) as ModelDoc2;
                    usedTemplate = defaultTemplate;
                }

                // Method 2: Search SolidWorks installation directory
                if (newPart == null)
                {
                    string swPath = swApp.GetExecutablePath();
                    if (!string.IsNullOrEmpty(swPath))
                    {
                        string swDir = System.IO.Path.GetDirectoryName(swPath);
                        
                        // Search for templates in various subfolders
                        string[] searchPaths = new string[]
                        {
                            System.IO.Path.Combine(swDir, "lang", "english", "Tutorial"),
                            System.IO.Path.Combine(swDir, "lang", "english"),
                            System.IO.Path.Combine(swDir, "lang", "italian", "Tutorial"),
                            System.IO.Path.Combine(swDir, "lang", "italian"),
                            System.IO.Path.Combine(swDir, "templates"),
                            swDir
                        };
                        
                        foreach (string searchPath in searchPaths)
                        {
                            if (System.IO.Directory.Exists(searchPath))
                            {
                                string[] templates = System.IO.Directory.GetFiles(searchPath, "*.prtdot");
                                if (templates.Length > 0)
                                {
                                    newPart = swApp.NewDocument(templates[0], 0, 0, 0) as ModelDoc2;
                                    if (newPart != null)
                                    {
                                        usedTemplate = templates[0];
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                // Method 3: Try ProgramData locations
                if (newPart == null)
                {
                    for (int year = 2030; year >= 2018; year--)
                    {
                        string path = $@"C:\ProgramData\SolidWorks\SOLIDWORKS {year}\templates\Part.prtdot";
                        if (System.IO.File.Exists(path))
                        {
                            newPart = swApp.NewDocument(path, 0, 0, 0) as ModelDoc2;
                            if (newPart != null)
                            {
                                usedTemplate = path;
                                break;
                            }
                        }
                    }
                }

                // Method 4: Search all drives for SolidWorks templates
                if (newPart == null)
                {
                    string templatesFolder = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swFileLocationsDocumentTemplates);
                    if (!string.IsNullOrEmpty(templatesFolder))
                    {
                        // Could be multiple paths separated by semicolon
                        string[] folders = templatesFolder.Split(';');
                        foreach (string folder in folders)
                        {
                            if (System.IO.Directory.Exists(folder.Trim()))
                            {
                                string[] prtFiles = System.IO.Directory.GetFiles(folder.Trim(), "*.prtdot", System.IO.SearchOption.AllDirectories);
                                foreach (string file in prtFiles)
                                {
                                    newPart = swApp.NewDocument(file, 0, 0, 0) as ModelDoc2;
                                    if (newPart != null)
                                    {
                                        usedTemplate = file;
                                        break;
                                    }
                                }
                                if (newPart != null) break;
                            }
                        }
                    }
                }

                // Method 5: Last resort - try to use empty string (some SW versions accept this)
                if (newPart == null)
                {
                    newPart = swApp.NewDocument("", (int)swDwgPaperSizes_e.swDwgPaperAsize, 0, 0) as ModelDoc2;
                }

                if (newPart == null)
                {
                    return "Error: Could not create new part. Please create one manually: File > New > Part";
                }

                // Zoom to fit
                newPart.ViewZoomtofit2();
                
                // If empty part requested or no type, we're done
                if (createEmpty)
                {
                    return "Success: Created new empty part.";
                }

                // Otherwise create the requested geometry
                string result = CreateFeature(type, parameters);
                if (result.StartsWith("Success"))
                {
                    return $"Success: Created new part with {type}.";
                }
                return result;
            }
            catch (Exception ex)
            {
                return $"Error creating part: {ex.Message}";
            }
        }

        #endregion

        #region Feature Creation

        private string CreateFeature(string type, Dictionary<string, object> parameters)
        {
            ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
            
            if (swModel == null)
            {
                return "Error: No active model. Please open or create a part document first.";
            }

            if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                return "Error: This command requires a Part document.";
            }

            PartDoc partDoc = swModel as PartDoc;
            swModel.ClearSelection2(true);

            switch (type)
                {
                    case "box":
                    case "rectangular":
                    case "rectangle":
                case "block":
                case "cube":
                    return CreateBox(partDoc, swModel, parameters);
                    
                    case "cylinder":
                    case "cylindrical":
                case "rod":
                case "circle":
                    return CreateCylinder(partDoc, swModel, parameters);

                case "boss_on_face":
                case "extrude_on_face":
                case "rectangle_on_face":
                    return CreateBossOnFace(partDoc, swModel, parameters);

                case "cut_on_face":
                case "pocket_on_face":
                    return CreateCutOnFace(partDoc, swModel, parameters);
                    
                    case "hole":
                case "simple_hole":
                    return CreateSimpleHole(partDoc, swModel, parameters);

                case "threaded_hole":
                case "tapped_hole":
                case "m4":
                case "m5":
                case "m6":
                case "m8":
                case "m10":
                    return CreateThreadedHole(partDoc, swModel, parameters, type);

                case "counterbore":
                case "counterbore_hole":
                    return CreateCounterboreHole(partDoc, swModel, parameters);

                case "countersink":
                case "countersink_hole":
                    return CreateCountersinkHole(partDoc, swModel, parameters);

                case "fillet":
                case "round":
                    return CreateFillet(partDoc, swModel, parameters);

                case "chamfer":
                    return CreateChamfer(partDoc, swModel, parameters);

                case "extrusion":
                case "extrude":
                case "boss":
                    return CreateExtrusion(partDoc, swModel, parameters);

                case "cut":
                case "cut_extrude":
                case "pocket":
                    return CreateCutExtrude(partDoc, swModel, parameters);

                case "pattern":
                case "linear_pattern":
                    return CreateLinearPattern(partDoc, swModel, parameters);

                case "circular_pattern":
                    return CreateCircularPattern(partDoc, swModel, parameters);

                case "shell":
                    return CreateShell(partDoc, swModel, parameters);
                    
                    default:
                    return $"Unknown feature type '{type}'. Supported: box, cylinder, hole, threaded_hole, fillet, chamfer, extrusion, cut, pattern, shell";
            }
        }

        /// <summary>
        /// Creates a rectangular box (extruded rectangle)
        /// </summary>
        private string CreateBox(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                // Get dimensions (default to mm)
                double width = GetDoubleParam(parameters, "width", 100.0);
                double height = GetDoubleParam(parameters, "height", 100.0);
                double depth = GetDoubleParam(parameters, "depth", 50.0);
                string units = GetStringParam(parameters, "units", "mm");

                // Handle alternative parameter names
                if (parameters != null)
                {
                    if (parameters.ContainsKey("length"))
                        width = GetDoubleParam(parameters, "length", width);
                    if (parameters.ContainsKey("x"))
                        width = GetDoubleParam(parameters, "x", width);
                    if (parameters.ContainsKey("y"))
                        height = GetDoubleParam(parameters, "y", height);
                    if (parameters.ContainsKey("z"))
                        depth = GetDoubleParam(parameters, "z", depth);
                }

                // Convert to meters
                double conversionFactor = GetConversionFactor(units);
                width *= conversionFactor;
                height *= conversionFactor;
                depth *= conversionFactor;

                // Select the front plane
                string planeName = GetStringParam(parameters, "plane", "Front Plane");
                bool selected = SelectPlane(swModel, planeName);
                if (!selected)
                {
                    return $"Error: Could not select {planeName}.";
                }

                // Insert sketch
                swModel.InsertSketch2(true);
                
                // Create rectangle centered at origin or at specified position
                double offsetX = GetDoubleParam(parameters, "offset_x", 0) * conversionFactor;
                double offsetY = GetDoubleParam(parameters, "offset_y", 0) * conversionFactor;

                // Draw corner rectangle
                swModel.CreateLine2(offsetX, offsetY, 0, offsetX + width, offsetY, 0);
                swModel.CreateLine2(offsetX + width, offsetY, 0, offsetX + width, offsetY + height, 0);
                swModel.CreateLine2(offsetX + width, offsetY + height, 0, offsetX, offsetY + height, 0);
                swModel.CreateLine2(offsetX, offsetY + height, 0, offsetX, offsetY, 0);

                // Exit sketch
                swModel.InsertSketch2(true);

                // Create extrusion
                FeatureManager featureMgr = swModel.FeatureManager;
                Feature extrudeFeature = featureMgr.FeatureExtrusion2(
                    true, false, false,
                    0, 0, // End conditions
                    depth, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    true, true, true,
                    0, 0,
                    false
                ) as Feature;

                if (extrudeFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    double w_mm = width / conversionFactor;
                    double h_mm = height / conversionFactor;
                    double d_mm = depth / conversionFactor;
                    return $"Success: Created box {w_mm:F1} x {h_mm:F1} x {d_mm:F1} {units}";
                }
                else
                {
                    return "Error: Failed to create extrusion. The sketch may not be valid.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating box: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a cylinder (extruded circle)
        /// </summary>
        private string CreateCylinder(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double radius = GetDoubleParam(parameters, "radius", 0);
                double diameter = GetDoubleParam(parameters, "diameter", 50.0);
                double height = GetDoubleParam(parameters, "height", 100.0);
                string units = GetStringParam(parameters, "units", "mm");

                // Use radius if provided, otherwise calculate from diameter
                if (radius <= 0)
                {
                    radius = diameter / 2.0;
                }

                double conversionFactor = GetConversionFactor(units);
                radius *= conversionFactor;
                height *= conversionFactor;

                // Select the front plane
                string planeName = GetStringParam(parameters, "plane", "Front Plane");
                SelectPlane(swModel, planeName);

                // Insert sketch
                swModel.InsertSketch2(true);
                
                // Create circle at origin
                double centerX = GetDoubleParam(parameters, "center_x", 0) * conversionFactor;
                double centerY = GetDoubleParam(parameters, "center_y", 0) * conversionFactor;

                // Create a circle
                swModel.CreateCircle2(centerX, centerY, 0, centerX + radius, centerY, 0);

                // Exit sketch
                swModel.InsertSketch2(true);

                // Create extrusion
                FeatureManager featureMgr = swModel.FeatureManager;
                Feature extrudeFeature = featureMgr.FeatureExtrusion2(
                    true, false, false,
                    0, 0,
                    height, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    true, true, true,
                    0, 0,
                    false
                ) as Feature;

                if (extrudeFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    double d_mm = (radius * 2) / conversionFactor;
                    double h_mm = height / conversionFactor;
                    return $"Success: Created cylinder Ø{d_mm:F1} x {h_mm:F1} {units}";
                }
                else
                {
                    return "Error: Failed to create cylinder extrusion.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating cylinder: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates an extruded boss on an existing face (like a rectangular boss on top of a cylinder)
        /// </summary>
        private string CreateBossOnFace(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double width = GetDoubleParam(parameters, "width", 10.0);
                double height = GetDoubleParam(parameters, "height", 10.0);
                double depth = GetDoubleParam(parameters, "depth", 5.0);
                string units = GetStringParam(parameters, "units", "mm");
                string face = GetStringParam(parameters, "face", "top");

                double conversionFactor = GetConversionFactor(units);
                width *= conversionFactor;
                height *= conversionFactor;
                depth *= conversionFactor;

                // Get offset position
                double offsetX = GetDoubleParam(parameters, "x", 0) * conversionFactor;
                double offsetY = GetDoubleParam(parameters, "y", 0) * conversionFactor;

                // Try to select the specified face
                if (!SelectFace(swModel, face))
                {
                    return $"Error: Could not select {face} face. Please select a planar face manually and try again.";
                }

                // Insert sketch on the selected face
                swModel.InsertSketch2(true);
                
                // Create centered rectangle
                double halfW = width / 2;
                double halfH = height / 2;
                swModel.CreateLine2(offsetX - halfW, offsetY - halfH, 0, offsetX + halfW, offsetY - halfH, 0);
                swModel.CreateLine2(offsetX + halfW, offsetY - halfH, 0, offsetX + halfW, offsetY + halfH, 0);
                swModel.CreateLine2(offsetX + halfW, offsetY + halfH, 0, offsetX - halfW, offsetY + halfH, 0);
                swModel.CreateLine2(offsetX - halfW, offsetY + halfH, 0, offsetX - halfW, offsetY - halfH, 0);

                // Exit sketch
                swModel.InsertSketch2(true);

                // Create extrusion
                FeatureManager featureMgr = swModel.FeatureManager;
                Feature extrudeFeature = featureMgr.FeatureExtrusion2(
                    true, false, false,
                    0, 0,
                    depth, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    true, true, true,
                    0, 0,
                    false
                ) as Feature;

                if (extrudeFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    double w_mm = width / conversionFactor;
                    double h_mm = height / conversionFactor;
                    double d_mm = depth / conversionFactor;
                    return $"Success: Created boss {w_mm:F1} x {h_mm:F1} x {d_mm:F1} {units} on {face} face";
                }
                else
                {
                    return "Error: Failed to create boss. Make sure a valid face is selected.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating boss on face: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a cut/pocket on an existing face
        /// </summary>
        private string CreateCutOnFace(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double width = GetDoubleParam(parameters, "width", 10.0);
                double height = GetDoubleParam(parameters, "height", 10.0);
                double depth = GetDoubleParam(parameters, "depth", 5.0);
                string units = GetStringParam(parameters, "units", "mm");
                string face = GetStringParam(parameters, "face", "top");
                bool throughAll = GetBoolParam(parameters, "through_all", false);

                double conversionFactor = GetConversionFactor(units);
                width *= conversionFactor;
                height *= conversionFactor;
                depth *= conversionFactor;

                double offsetX = GetDoubleParam(parameters, "x", 0) * conversionFactor;
                double offsetY = GetDoubleParam(parameters, "y", 0) * conversionFactor;

                // Try to select the specified face
                if (!SelectFace(swModel, face))
                {
                    return $"Error: Could not select {face} face. Please select a planar face manually.";
                }

                // Insert sketch on the selected face
                swModel.InsertSketch2(true);

                // Create centered rectangle
                double halfW = width / 2;
                double halfH = height / 2;
                swModel.CreateLine2(offsetX - halfW, offsetY - halfH, 0, offsetX + halfW, offsetY - halfH, 0);
                swModel.CreateLine2(offsetX + halfW, offsetY - halfH, 0, offsetX + halfW, offsetY + halfH, 0);
                swModel.CreateLine2(offsetX + halfW, offsetY + halfH, 0, offsetX - halfW, offsetY + halfH, 0);
                swModel.CreateLine2(offsetX - halfW, offsetY + halfH, 0, offsetX - halfW, offsetY - halfH, 0);

                // Exit sketch
                swModel.InsertSketch2(true);

                // Create cut
                FeatureManager featureMgr = swModel.FeatureManager;
                int endCondition = throughAll ? (int)swEndConditions_e.swEndCondThroughAll : (int)swEndConditions_e.swEndCondBlind;

                Feature cutFeature = featureMgr.FeatureCut4(
                    true, false, false,
                    endCondition, 0,
                    depth, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    false, true, true,
                    true, true,
                    false, 0,
                    0, false, false
                ) as Feature;

                if (cutFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    double w_mm = width / conversionFactor;
                    double h_mm = height / conversionFactor;
                    string depthStr = throughAll ? "through all" : $"{depth / conversionFactor:F1}{units} deep";
                    return $"Success: Created {w_mm:F1} x {h_mm:F1} {units} cut on {face} face, {depthStr}";
                }
                else
                {
                    return "Error: Failed to create cut. Make sure sketch is on a valid face.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating cut on face: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a simple hole
        /// </summary>
        private string CreateSimpleHole(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double diameter = GetDoubleParam(parameters, "diameter", 10.0);
                double depth = GetDoubleParam(parameters, "depth", 20.0);
                string units = GetStringParam(parameters, "units", "mm");
                bool throughAll = GetBoolParam(parameters, "through_all", false);

                double conversionFactor = GetConversionFactor(units);
                double radius = (diameter / 2.0) * conversionFactor;
                depth *= conversionFactor;

                // Get position
                double x = GetDoubleParam(parameters, "x", 0) * conversionFactor;
                double y = GetDoubleParam(parameters, "y", 0) * conversionFactor;

                // Select face (use top face by default, or specified face)
                string face = GetStringParam(parameters, "face", "top");
                if (!SelectFace(swModel, face))
                {
                    return "Error: Could not select face for hole. Please select a face manually.";
                }

                // Insert sketch on the selected face
                swModel.InsertSketch2(true);

                // Create circle for hole
                swModel.CreateCircle2(x, y, 0, x + radius, y, 0);

                // Exit sketch
                swModel.InsertSketch2(true);

                // Create cut extrusion
                FeatureManager featureMgr = swModel.FeatureManager;
                
                int endCondition = throughAll ? (int)swEndConditions_e.swEndCondThroughAll : (int)swEndConditions_e.swEndCondBlind;
                
                Feature cutFeature = featureMgr.FeatureCut4(
                    true, false, false,
                    endCondition, 0,
                    depth, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    false, true, true,
                    true, true,
                    false, 0,
                    0, false, false
                ) as Feature;

                if (cutFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    string depthStr = throughAll ? "through all" : $"{depth / conversionFactor:F1} {units} deep";
                    return $"Success: Created Ø{diameter:F1} {units} hole, {depthStr}";
                }
                else
                {
                    return "Error: Failed to create hole. Ensure a face is selected.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating hole: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a threaded hole (M4, M6, etc.)
        /// </summary>
        private string CreateThreadedHole(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters, string type)
        {
            try
            {
                // Parse thread size from type or parameters
                string threadSize = GetStringParam(parameters, "thread_size", "");
                if (string.IsNullOrEmpty(threadSize))
                {
                    // Try to extract from type (m4, m6, etc.)
                    if (type.StartsWith("m") && type.Length >= 2)
                    {
                        threadSize = type.ToUpper();
                    }
                    else
                    {
                        threadSize = "M6"; // Default
                    }
                }

                // Get thread parameters
                double depth = GetDoubleParam(parameters, "depth", 15.0);
                string units = GetStringParam(parameters, "units", "mm");
                bool throughAll = GetBoolParam(parameters, "through_all", false);
                int count = GetIntParam(parameters, "count", 1);

                double conversionFactor = GetConversionFactor(units);
                depth *= conversionFactor;

                // Standard metric thread dimensions (tap drill sizes)
                Dictionary<string, double> tapDrillSizes = new Dictionary<string, double>
                {
                    { "M2", 1.6 },
                    { "M2.5", 2.05 },
                    { "M3", 2.5 },
                    { "M4", 3.3 },
                    { "M5", 4.2 },
                    { "M6", 5.0 },
                    { "M8", 6.8 },
                    { "M10", 8.5 },
                    { "M12", 10.2 },
                    { "M14", 12.0 },
                    { "M16", 14.0 },
                    { "M20", 17.5 }
                };

                double tapDrill = 5.0; // Default M6
                if (tapDrillSizes.ContainsKey(threadSize.ToUpper()))
                {
                    tapDrill = tapDrillSizes[threadSize.ToUpper()];
                }

                double radius = (tapDrill / 2.0) / 1000.0; // Convert to meters

                // Get positions for multiple holes
                List<Tuple<double, double>> positions = new List<Tuple<double, double>>();
                
                if (count == 1)
                {
                    double x = GetDoubleParam(parameters, "x", 0) * conversionFactor;
                    double y = GetDoubleParam(parameters, "y", 0) * conversionFactor;
                    positions.Add(new Tuple<double, double>(x, y));
                }
                else
                {
                    // Create pattern based on spacing
                    double spacing = GetDoubleParam(parameters, "spacing", 20.0) * conversionFactor;
                    double startX = GetDoubleParam(parameters, "x", 0) * conversionFactor;
                    double startY = GetDoubleParam(parameters, "y", 0) * conversionFactor;
                    
                    for (int i = 0; i < count; i++)
                    {
                        positions.Add(new Tuple<double, double>(startX + (i * spacing), startY));
                    }
                }

                // Select face
                string face = GetStringParam(parameters, "face", "top");
                if (!SelectFace(swModel, face))
                {
                    return "Error: Could not select face. Please select a planar face on the model.";
                }

                // Insert sketch
                swModel.InsertSketch2(true);
                
                // Create circles for all holes
                foreach (var pos in positions)
                {
                    swModel.CreateCircle2(pos.Item1, pos.Item2, 0, pos.Item1 + radius, pos.Item2, 0);
                }

                // Exit sketch
                swModel.InsertSketch2(true);

                // Create cut
                FeatureManager featureMgr = swModel.FeatureManager;
                int endCondition = throughAll ? (int)swEndConditions_e.swEndCondThroughAll : (int)swEndConditions_e.swEndCondBlind;

                Feature cutFeature = featureMgr.FeatureCut4(
                    true, false, false,
                    endCondition, 0,
                    depth, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    false, true, true,
                    true, true,
                    false, 0,
                    0, false, false
                ) as Feature;

                if (cutFeature != null)
                {
                    // Rename the feature to indicate it's a threaded hole
                    cutFeature.Name = $"{threadSize} Threaded Hole";
                    
                    swModel.ViewZoomtofit2();
                    string depthStr = throughAll ? "through all" : $"{depth / conversionFactor:F1} {units} deep";
                    string countStr = count > 1 ? $"{count}x " : "";
                    return $"Success: Created {countStr}{threadSize} threaded hole(s) (Ø{tapDrill:F1}mm tap drill), {depthStr}";
                }
                else
                {
                    return "Error: Failed to create threaded hole.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating threaded hole: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a counterbore hole
        /// </summary>
        private string CreateCounterboreHole(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double holeDiameter = GetDoubleParam(parameters, "hole_diameter", 6.0);
                double boreDiameter = GetDoubleParam(parameters, "bore_diameter", 12.0);
                double boreDepth = GetDoubleParam(parameters, "bore_depth", 5.0);
                double holeDepth = GetDoubleParam(parameters, "hole_depth", 20.0);
                string units = GetStringParam(parameters, "units", "mm");
                bool throughAll = GetBoolParam(parameters, "through_all", false);

                double conversionFactor = GetConversionFactor(units);
                double holeRadius = (holeDiameter / 2.0) * conversionFactor;
                double boreRadius = (boreDiameter / 2.0) * conversionFactor;
                boreDepth *= conversionFactor;
                holeDepth *= conversionFactor;

                double x = GetDoubleParam(parameters, "x", 0) * conversionFactor;
                double y = GetDoubleParam(parameters, "y", 0) * conversionFactor;

                // Select face
                if (!SelectFace(swModel, "top"))
                {
                    return "Error: Could not select face.";
                }

                // Create counterbore (large circle first)
                swModel.InsertSketch2(true);
                swModel.CreateCircle2(x, y, 0, x + boreRadius, y, 0);
                swModel.InsertSketch2(true);

                FeatureManager featureMgr = swModel.FeatureManager;
                featureMgr.FeatureCut4(
                    true, false, false, 0, 0,
                    boreDepth, 0,
                    false, false, false, false,
                    0, 0, false, false, false, false,
                    false, true, true, true, true,
                    false, 0, 0, false, false
                );

                // Create through hole
                SelectFace(swModel, "top");
                swModel.InsertSketch2(true);
                swModel.CreateCircle2(x, y, 0, x + holeRadius, y, 0);
                swModel.InsertSketch2(true);

                int endCondition = throughAll ? (int)swEndConditions_e.swEndCondThroughAll : (int)swEndConditions_e.swEndCondBlind;
                featureMgr.FeatureCut4(
                    true, false, false, endCondition, 0,
                    holeDepth, 0,
                    false, false, false, false,
                    0, 0, false, false, false, false,
                    false, true, true, true, true,
                    false, 0, 0, false, false
                );

                swModel.ViewZoomtofit2();
                return $"Success: Created counterbore hole Ø{holeDiameter}mm with Ø{boreDiameter}mm x {boreDepth / conversionFactor}mm counterbore";
            }
            catch (Exception ex)
            {
                return $"Error creating counterbore: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a countersink hole
        /// </summary>
        private string CreateCountersinkHole(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            // Simplified implementation - would need more complex geometry for true countersink
            return CreateCounterboreHole(partDoc, swModel, parameters);
        }

        /// <summary>
        /// Creates a fillet on selected edges
        /// </summary>
        private string CreateFillet(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double radius = GetDoubleParam(parameters, "radius", 5.0);
                string units = GetStringParam(parameters, "units", "mm");

                double conversionFactor = GetConversionFactor(units);
                radius *= conversionFactor;

                FeatureManager featureMgr = swModel.FeatureManager;

                // Check if edges are pre-selected
                SelectionMgr selMgr = swModel.SelectionManager as SelectionMgr;
                if (selMgr.GetSelectedObjectCount2(-1) == 0)
                {
                    return "Error: Please select one or more edges first, then run the fillet command.";
                }

                // Create fillet
                Feature filletFeature = featureMgr.FeatureFillet3(
                    195, // Options
                    radius,
                    0, 0, 0, 0, 0,
                    null, null, null, null, null, null, null
                ) as Feature;

                if (filletFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    return $"Success: Created fillet with R{radius / conversionFactor:F1} {units}";
                }
                else
                {
                    return "Error: Failed to create fillet. Ensure edges are selected.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating fillet: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a chamfer on selected edges
        /// </summary>
        private string CreateChamfer(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double distance = GetDoubleParam(parameters, "distance", 2.0);
                double angle = GetDoubleParam(parameters, "angle", 45.0);
                string units = GetStringParam(parameters, "units", "mm");

                double conversionFactor = GetConversionFactor(units);
                distance *= conversionFactor;
                double angleRad = angle * Math.PI / 180.0;

                FeatureManager featureMgr = swModel.FeatureManager;

                // Check if edges are pre-selected
                SelectionMgr selMgr = swModel.SelectionManager as SelectionMgr;
                if (selMgr.GetSelectedObjectCount2(-1) == 0)
                {
                    return "Error: Please select one or more edges first, then run the chamfer command.";
                }

                // Create chamfer
                Feature chamferFeature = featureMgr.InsertFeatureChamfer(
                    4, // Chamfer type
                    (int)swChamferType_e.swChamferAngleDistance,
                    distance, angleRad,
                    0, 0, 0, 0
                ) as Feature;

                if (chamferFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    return $"Success: Created chamfer {distance / conversionFactor:F1} {units} x {angle}°";
                }
                else
                {
                    return "Error: Failed to create chamfer. Ensure edges are selected.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating chamfer: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates an extrusion from selected sketch or creates new sketch
        /// </summary>
        private string CreateExtrusion(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double depth = GetDoubleParam(parameters, "depth", 25.0);
                string units = GetStringParam(parameters, "units", "mm");
                bool both = GetBoolParam(parameters, "both_directions", false);

                double conversionFactor = GetConversionFactor(units);
                depth *= conversionFactor;

                FeatureManager featureMgr = swModel.FeatureManager;

                Feature extrudeFeature = featureMgr.FeatureExtrusion2(
                    true, false, both,
                    0, 0,
                    depth, both ? depth : 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    true, true, true,
                    0, 0,
                    false
                ) as Feature;

                if (extrudeFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    return $"Success: Created extrusion {depth / conversionFactor:F1} {units}";
                }
                else
                {
                    return "Error: Failed to create extrusion. Ensure a closed sketch is selected.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating extrusion: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a cut extrusion
        /// </summary>
        private string CreateCutExtrude(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double depth = GetDoubleParam(parameters, "depth", 10.0);
                string units = GetStringParam(parameters, "units", "mm");
                bool throughAll = GetBoolParam(parameters, "through_all", false);

                double conversionFactor = GetConversionFactor(units);
                depth *= conversionFactor;

                FeatureManager featureMgr = swModel.FeatureManager;
                int endCondition = throughAll ? (int)swEndConditions_e.swEndCondThroughAll : (int)swEndConditions_e.swEndCondBlind;

                Feature cutFeature = featureMgr.FeatureCut4(
                    true, false, false,
                    endCondition, 0,
                    depth, 0,
                    false, false, false, false,
                    0, 0,
                    false, false, false, false,
                    false, true, true,
                    true, true,
                    false, 0,
                    0, false, false
                ) as Feature;

                if (cutFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    string depthStr = throughAll ? "through all" : $"{depth / conversionFactor:F1} {units}";
                    return $"Success: Created cut extrusion {depthStr}";
                }
                else
                {
                    return "Error: Failed to create cut. Ensure a closed sketch on a face is selected.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating cut: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a linear pattern
        /// Note: Pattern creation requires specific selections in SolidWorks
        /// </summary>
        private string CreateLinearPattern(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                int countX = GetIntParam(parameters, "count_x", 2);
                int countY = GetIntParam(parameters, "count_y", 1);
                double spacingX = GetDoubleParam(parameters, "spacing_x", 20.0);
                double spacingY = GetDoubleParam(parameters, "spacing_y", 20.0);
                string units = GetStringParam(parameters, "units", "mm");

                // Check if features are selected
                SelectionMgr selMgr = swModel.SelectionManager as SelectionMgr;
                if (selMgr.GetSelectedObjectCount2(-1) == 0)
                {
                    return "Error: Please select the feature(s) to pattern first, then an edge for direction.";
                }

                // Linear pattern requires complex setup - guide the user
                return $"To create a {countX}x{countY} linear pattern with {spacingX}{units} x {spacingY}{units} spacing:\n" +
                       $"1. Select the feature to pattern in the Feature Tree\n" +
                       $"2. Go to Insert > Pattern/Mirror > Linear Pattern\n" +
                       $"3. Set Direction 1: {countX} instances, {spacingX}{units} spacing\n" +
                       $"4. Set Direction 2: {countY} instances, {spacingY}{units} spacing\n" +
                       $"(Automatic pattern creation requires specific SolidWorks version)";
            }
            catch (Exception ex)
            {
                return $"Error creating pattern: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a circular pattern
        /// Note: Circular pattern creation requires specific selections in SolidWorks
        /// </summary>
        private string CreateCircularPattern(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                int count = GetIntParam(parameters, "count", 6);
                double angle = GetDoubleParam(parameters, "angle", 360.0);
                bool equalSpacing = GetBoolParam(parameters, "equal_spacing", true);

                SelectionMgr selMgr = swModel.SelectionManager as SelectionMgr;
                if (selMgr.GetSelectedObjectCount2(-1) == 0)
                {
                    return "Error: Please select the feature(s) to pattern first, then an axis.";
                }

                string spacingType = equalSpacing ? "equal spacing" : $"{angle / count:F1}° between instances";

                // Circular pattern requires complex setup - guide the user
                return $"To create a circular pattern with {count} instances over {angle}°:\n" +
                       $"1. Select the feature to pattern in the Feature Tree\n" +
                       $"2. Go to Insert > Pattern/Mirror > Circular Pattern\n" +
                       $"3. Select an axis or cylindrical face for the pattern axis\n" +
                       $"4. Set {count} instances with {spacingType}\n" +
                       $"(Automatic circular pattern creation requires specific SolidWorks version)";
            }
            catch (Exception ex)
            {
                return $"Error creating circular pattern: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a shell feature
        /// Note: Shell creation requires face selection in SolidWorks
        /// </summary>
        private string CreateShell(PartDoc partDoc, ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double thickness = GetDoubleParam(parameters, "thickness", 2.0);
                string units = GetStringParam(parameters, "units", "mm");
                bool outward = GetBoolParam(parameters, "outward", false);

                // Check if faces are selected (required for shell)
                SelectionMgr selMgr = swModel.SelectionManager as SelectionMgr;
                if (selMgr.GetSelectedObjectCount2(-1) == 0)
                {
                    return $"To create a shell with {thickness}{units} wall thickness:\n" +
                           $"1. Select the face(s) to remove (open faces)\n" +
                           $"2. Go to Insert > Features > Shell\n" +
                           $"3. Set wall thickness to {thickness}{units}\n" +
                           $"4. Check 'Shell outward' if needed: {(outward ? "Yes" : "No")}";
                }

                // Try to use the model's extension to insert shell
                string direction = outward ? "outward" : "inward";
                return $"Shell feature setup:\n" +
                       $"- Wall thickness: {thickness}{units}\n" +
                       $"- Direction: {direction}\n" +
                       $"- Selected faces will be removed\n" +
                       $"Go to Insert > Features > Shell to complete.";
            }
            catch (Exception ex)
            {
                return $"Error creating shell: {ex.Message}";
            }
        }

        #endregion

        #region Modification Methods

        private string ModifyFeature(string type, Dictionary<string, object> parameters)
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                if (swModel == null)
                {
                    return "Error: No active model.";
                }

                switch (type)
                {
                    case "dimension":
                    case "size":
                    case "length":
                        return ModifyDimension(swModel, parameters);

                    case "scale":
                        return ScaleModel(swModel, parameters);

                    default:
                        return $"Unknown modification type '{type}'.";
                }
            }
            catch (Exception ex)
            {
                return $"Error modifying: {ex.Message}";
            }
        }

        /// <summary>
        /// Modifies a dimension in the model
        /// </summary>
        private string ModifyDimension(ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                string dimName = GetStringParam(parameters, "dimension_name", "");
                double newValue = GetDoubleParam(parameters, "value", 0);
                double delta = GetDoubleParam(parameters, "delta", 0);
                string units = GetStringParam(parameters, "units", "mm");

                double conversionFactor = GetConversionFactor(units);

                if (!string.IsNullOrEmpty(dimName))
                {
                    // Get dimension by name
                    Dimension dim = swModel.Parameter(dimName) as Dimension;
                    if (dim != null)
                    {
                        double currentValue = dim.SystemValue;
                        
                        if (delta != 0)
                        {
                            newValue = currentValue + (delta * conversionFactor);
                        }
                        else
                        {
                            newValue *= conversionFactor;
                        }

                        dim.SystemValue = newValue;
                        swModel.EditRebuild3();
                        swModel.ViewZoomtofit2();
                        
                        return $"Success: Changed {dimName} to {newValue / conversionFactor:F2} {units}";
                    }
                    else
                    {
                        return $"Error: Dimension '{dimName}' not found.";
                    }
                }
                else
                {
                    // Try to modify selected dimension
                    SelectionMgr selMgr = swModel.SelectionManager as SelectionMgr;
                    if (selMgr.GetSelectedObjectCount2(-1) > 0)
                    {
                        object selObj = selMgr.GetSelectedObject6(1, -1);
                        DisplayDimension dispDim = selObj as DisplayDimension;
                        if (dispDim != null)
                        {
                            Dimension dim = dispDim.GetDimension2(0) as Dimension;
                            if (dim != null)
                            {
                                double currentValue = dim.SystemValue;
                                
                                if (delta != 0)
                                {
                                    newValue = currentValue + (delta * conversionFactor);
                                }
                                else
                                {
                                    newValue *= conversionFactor;
                                }

                                dim.SystemValue = newValue;
                                swModel.EditRebuild3();
                                swModel.ViewZoomtofit2();
                                
                                return $"Success: Modified dimension to {newValue / conversionFactor:F2} {units}";
                            }
                        }
                    }
                    
                    return "Error: No dimension selected. Please select a dimension or provide dimension_name.";
                }
            }
            catch (Exception ex)
            {
                return $"Error modifying dimension: {ex.Message}";
            }
        }

        /// <summary>
        /// Scales the model uniformly
        /// Note: Scale feature is limited in SolidWorks API, this modifies dimensions instead
        /// </summary>
        private string ScaleModel(ModelDoc2 swModel, Dictionary<string, object> parameters)
        {
            try
            {
                double factor = GetDoubleParam(parameters, "factor", 1.0);

                if (factor <= 0)
                {
                    return "Error: Scale factor must be positive.";
                }

                if (Math.Abs(factor - 1.0) < 0.001)
                {
                    return "No scaling needed (factor = 1.0)";
                }

                // Scale is complex in SolidWorks - recommend using dimension modification instead
                // For now, return a helpful message
                return $"Scale feature requires manual application in SolidWorks. " +
                       $"Suggested scale factor: {factor:F2}x. " +
                       $"Use Insert > Features > Scale or modify individual dimensions.";
            }
            catch (Exception ex)
            {
                return $"Error scaling: {ex.Message}";
            }
        }

        private string DeleteFeature(Dictionary<string, object> parameters)
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                if (swModel == null)
                {
                    return "Error: No active model.";
                }

                string featureName = GetStringParam(parameters, "feature_name", "");

                if (!string.IsNullOrEmpty(featureName))
                {
                    bool selected = swModel.Extension.SelectByID2(featureName, "BODYFEATURE", 0, 0, 0, false, 0, null, 0);
                    if (!selected)
                    {
                        return $"Error: Could not find feature '{featureName}'.";
                    }
                }

                SelectionMgr selMgr = swModel.SelectionManager as SelectionMgr;
                if (selMgr.GetSelectedObjectCount2(-1) == 0)
                {
                    return "Error: No feature selected to delete.";
                }

                swModel.EditDelete();
                swModel.EditRebuild3();
                
                return "Success: Deleted selected feature(s).";
            }
            catch (Exception ex)
            {
                return $"Error deleting: {ex.Message}";
            }
        }

        #endregion

        #region Helper Methods

        private bool SelectPlane(ModelDoc2 swModel, string planeName)
        {
            // Try common plane names
            string[] planeVariants = { planeName, $"{planeName}", $"Plan {planeName}", planeName.Replace(" ", "") };
            
            foreach (string name in planeVariants)
            {
                if (swModel.Extension.SelectByID2(name, "PLANE", 0, 0, 0, false, 0, null, 0))
                {
                    return true;
                }
            }

            // Try localized names (French, German, etc.)
            string[] alternateNames = GetAlternatePlaneNames(planeName);
            foreach (string name in alternateNames)
            {
                if (swModel.Extension.SelectByID2(name, "PLANE", 0, 0, 0, false, 0, null, 0))
                {
                    return true;
                }
            }

            return false;
        }

        private string[] GetAlternatePlaneNames(string planeName)
        {
            switch (planeName.ToLower())
            {
                case "front plane":
                case "front":
                    return new[] { "Plan de face", "Vorderseite", "Front", "Front Plane", "Plane1" };
                case "top plane":
                case "top":
                    return new[] { "Plan de dessus", "Oben", "Top", "Top Plane", "Plane2" };
                case "right plane":
                case "right":
                    return new[] { "Plan de droite", "Rechts", "Right", "Right Plane", "Plane3" };
                default:
                    return new string[0];
            }
        }

        private bool SelectFace(ModelDoc2 swModel, string faceDescription)
        {
            try
            {
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    return false;
                }

                PartDoc partDoc = swModel as PartDoc;
                object bodiesObj = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, false);
                
                if (bodiesObj == null) return false;
                
                object[] bodies = bodiesObj as object[];
                if (bodies == null || bodies.Length == 0) return false;

                Body2 body = bodies[0] as Body2;
                if (body == null) return false;

                object[] faces = body.GetFaces() as object[];
                if (faces == null) return false;

                // Find the appropriate face based on description
                Face2 targetFace = null;
                double maxZ = double.MinValue;
                double minZ = double.MaxValue;

                foreach (Face2 face in faces)
                {
                    Surface surface = face.GetSurface() as Surface;
                    if (surface != null && surface.IsPlane())
                    {
                        // Get face normal to determine orientation
                        object normalObj = face.Normal;
                        if (normalObj != null)
                        {
                            double[] normal = normalObj as double[];
                            if (normal != null && normal.Length >= 3)
                            {
                                // Get UV range to find center point
                                double[] uvRange = face.GetUVBounds() as double[];
                                if (uvRange != null)
                                {
                                    double u = (uvRange[0] + uvRange[1]) / 2;
                                    double v = (uvRange[2] + uvRange[3]) / 2;
                                    
                                    object evalResult = surface.Evaluate(u, v, 0, 0);
                                    if (evalResult != null)
                                    {
                                        double[] point = evalResult as double[];
                                        if (point != null && point.Length >= 3)
                                        {
                                            // Top face has +Z normal and highest Z
                                            if (faceDescription.ToLower() == "top" && normal[2] > 0.9 && point[2] > maxZ)
                                            {
                                                maxZ = point[2];
                                                targetFace = face;
                                            }
                                            // Bottom face has -Z normal
                                            else if (faceDescription.ToLower() == "bottom" && normal[2] < -0.9 && point[2] < minZ)
                                            {
                                                minZ = point[2];
                                                targetFace = face;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Select the target face using ISelect interface
                if (targetFace != null)
                {
                    // Cast to Entity for selection
                    Entity entity = targetFace as Entity;
                    if (entity != null)
                    {
                        return entity.Select4(false, null);
                    }
                }

                // Fallback: select first planar face
                foreach (Face2 face in faces)
                {
                    Surface surface = face.GetSurface() as Surface;
                    if (surface != null && surface.IsPlane())
                    {
                        Entity entity = face as Entity;
                        if (entity != null)
                        {
                            return entity.Select4(false, null);
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private double GetConversionFactor(string units)
        {
            switch (units.ToLower())
            {
                case "mm":
                case "millimeter":
                case "millimeters":
                    return 0.001;
                case "cm":
                case "centimeter":
                case "centimeters":
                    return 0.01;
                case "m":
                case "meter":
                case "meters":
                    return 1.0;
                case "in":
                case "inch":
                case "inches":
                    return 0.0254;
                case "ft":
                case "foot":
                case "feet":
                    return 0.3048;
                default:
                    return 0.001; // Default to mm
            }
        }

        private double GetDoubleParam(Dictionary<string, object> parameters, string key, double defaultValue)
        {
            if (parameters == null || !parameters.ContainsKey(key))
                return defaultValue;

                object value = parameters[key];
            
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is float f) return f;
            if (value is string s && double.TryParse(s, out double result)) return result;
            
            return defaultValue;
        }

        private int GetIntParam(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            if (parameters == null || !parameters.ContainsKey(key))
                return defaultValue;

            object value = parameters[key];
            
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is string s && int.TryParse(s, out int result)) return result;
            
            return defaultValue;
        }

        private string GetStringParam(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (parameters == null || !parameters.ContainsKey(key))
                return defaultValue;

            return parameters[key]?.ToString() ?? defaultValue;
        }

        private bool GetBoolParam(Dictionary<string, object> parameters, string key, bool defaultValue)
        {
            if (parameters == null || !parameters.ContainsKey(key))
                return defaultValue;

                object value = parameters[key];
            
            if (value is bool b) return b;
            if (value is string s) return s.ToLower() == "true" || s == "1";
            if (value is int i) return i != 0;
            
            return defaultValue;
        }

        #endregion
    }
}
