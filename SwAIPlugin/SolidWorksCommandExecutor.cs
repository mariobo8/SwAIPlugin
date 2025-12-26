using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwAIPlugin
{
    /// <summary>
    /// Executes SolidWorks commands parsed from AI responses
    /// </summary>
    public class SolidWorksCommandExecutor
    {
        private SldWorks swApp;

        public SolidWorksCommandExecutor(SldWorks app)
        {
            swApp = app;
        }

        /// <summary>
        /// Executes a command based on parsed JSON structure
        /// </summary>
        public string ExecuteCommand(string action, string type, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                
                if (swModel == null)
                {
                    return "Error: No active model. Please open a part document first.";
                }

                // Ensure we're in a part document
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    return "Error: This command requires a Part document. Please open or create a part.";
                }

                PartDoc partDoc = swModel as PartDoc;
                swModel.ClearSelection2(true);

                switch (action.ToLower())
                {
                    case "create_feature":
                        return CreateFeature(partDoc, type, parameters);
                    
                    case "create":
                        return CreateFeature(partDoc, type, parameters);
                    
                    default:
                        return $"Error: Unknown action '{action}'. Supported actions: create_feature, create";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }

        private string CreateFeature(PartDoc partDoc, string type, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                switch (type.ToLower())
                {
                    case "box":
                    case "rectangular":
                    case "rectangle":
                        return CreateBox(partDoc, parameters);
                    
                    case "cylinder":
                    case "cylindrical":
                        return CreateCylinder(partDoc, parameters);
                    
                    case "hole":
                        return CreateHole(partDoc, parameters);
                    
                    default:
                        return $"Error: Unknown feature type '{type}'. Supported types: box, cylinder, hole";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating feature: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a rectangular box (extruded rectangle)
        /// </summary>
        private string CreateBox(PartDoc partDoc, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                ModelDoc2 swModel = partDoc as ModelDoc2;
                
                // Get dimensions (default to mm, convert if needed)
                double width = GetDoubleParameter(parameters, "width", 100.0);
                double height = GetDoubleParameter(parameters, "height", 100.0);
                double depth = GetDoubleParameter(parameters, "depth", 100.0);
                string units = GetStringParameter(parameters, "units", "mm");
                
                // Convert to meters (SolidWorks uses meters internally)
                if (units.ToLower() == "mm")
                {
                    width /= 1000.0;
                    height /= 1000.0;
                    depth /= 1000.0;
                }
                else if (units.ToLower() == "cm")
                {
                    width /= 100.0;
                    height /= 100.0;
                    depth /= 100.0;
                }
                else if (units.ToLower() == "in" || units.ToLower() == "inch")
                {
                    width *= 0.0254; // inches to meters
                    height *= 0.0254;
                    depth *= 0.0254;
                }

                // Select the front plane
                bool success = swModel.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
                if (!success)
                {
                    return "Error: Could not select Front Plane. Make sure you're in a part document.";
                }

                // Insert a new sketch on the front plane
                swModel.InsertSketch2(true);
                
                // Create a rectangle using CreateLine2
                // Start at origin, create rectangle
                swModel.CreateLine2(0, 0, 0, width, 0, 0);
                swModel.CreateLine2(width, 0, 0, width, height, 0);
                swModel.CreateLine2(width, height, 0, 0, height, 0);
                swModel.CreateLine2(0, height, 0, 0, 0, 0);

                // Exit sketch
                swModel.InsertSketch2(true);

                // Get the feature manager
                FeatureManager featureMgr = swModel.FeatureManager;
                
                // Use simpler FeatureExtrusion method
                // Parameters: endCondition, direction, depth, draftAngle, reverseDir
                bool endCondition = true; // Blind
                int direction = 1; // One direction  
                double depthValue = depth;
                double draftAngle = 0;
                bool reverseDir = false;

                Feature extrudeFeature = featureMgr.FeatureExtrusion(
                    endCondition, direction, depthValue, draftAngle, reverseDir
                ) as Feature;

                if (extrudeFeature != null)
                {
                    swModel.ViewZoomtofit2();
                    return $"Success: Created box ({width * 1000:F1} x {height * 1000:F1} x {depth * 1000:F1} mm)";
                }
                else
                {
                    return "Error: Failed to create extrude feature. Make sure the sketch is closed.";
                }
            }
            catch (Exception ex)
            {
                return $"Error creating box: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a cylinder (revolved circle)
        /// </summary>
        private string CreateCylinder(PartDoc partDoc, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                ModelDoc2 swModel = partDoc as ModelDoc2;
                
                // Get dimensions
                double radius = GetDoubleParameter(parameters, "radius", 50.0);
                double height = GetDoubleParameter(parameters, "height", 100.0);
                string units = GetStringParameter(parameters, "units", "mm");
                
                // Convert to meters
                if (units.ToLower() == "mm")
                {
                    radius /= 1000.0;
                    height /= 1000.0;
                }
                else if (units.ToLower() == "cm")
                {
                    radius /= 100.0;
                    height /= 100.0;
                }
                else if (units.ToLower() == "in" || units.ToLower() == "inch")
                {
                    radius *= 0.0254;
                    height *= 0.0254;
                }

                // Select the front plane
                bool success = swModel.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
                if (!success)
                {
                    return "Error: Could not select Front Plane.";
                }

                // Insert sketch
                swModel.InsertSketch2(true);
                
                // Create a rectangle for the revolve (width = radius, height = cylinder height)
                swModel.CreateLine2(0, 0, 0, radius, 0, 0);
                swModel.CreateLine2(radius, 0, 0, radius, height, 0);
                swModel.CreateLine2(radius, height, 0, 0, height, 0);
                swModel.CreateLine2(0, height, 0, 0, 0, 0);

                // Exit sketch
                swModel.InsertSketch2(true);

                // Create revolve feature (simplified - would need axis selection in real implementation)
                // For now, return a message that this needs more complex implementation
                return $"Cylinder creation requires axis selection. Dimensions: Radius={radius * 1000:F1}mm, Height={height * 1000:F1}mm. (Implementation in progress)";
            }
            catch (Exception ex)
            {
                return $"Error creating cylinder: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a hole feature
        /// </summary>
        private string CreateHole(PartDoc partDoc, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                ModelDoc2 swModel = partDoc as ModelDoc2;
                
                double diameter = GetDoubleParameter(parameters, "diameter", 10.0);
                double depth = GetDoubleParameter(parameters, "depth", 50.0);
                string units = GetStringParameter(parameters, "units", "mm");
                
                // Convert to meters
                if (units.ToLower() == "mm")
                {
                    diameter /= 1000.0;
                    depth /= 1000.0;
                }
                else if (units.ToLower() == "cm")
                {
                    diameter /= 100.0;
                    depth /= 100.0;
                }
                else if (units.ToLower() == "in" || units.ToLower() == "inch")
                {
                    diameter *= 0.0254;
                    depth *= 0.0254;
                }

                // Hole creation requires face selection - simplified for now
                return $"Hole creation requires face selection. Dimensions: Diameter={diameter * 1000:F1}mm, Depth={depth * 1000:F1}mm. (Implementation in progress)";
            }
            catch (Exception ex)
            {
                return $"Error creating hole: {ex.Message}";
            }
        }

        // Helper methods
        private double GetDoubleParameter(System.Collections.Generic.Dictionary<string, object> parameters, string key, double defaultValue)
        {
            if (parameters != null && parameters.ContainsKey(key))
            {
                object value = parameters[key];
                if (value is double)
                    return (double)value;
                if (value is int)
                    return (double)(int)value;
                if (value is string)
                {
                    if (double.TryParse((string)value, out double result))
                        return result;
                }
            }
            return defaultValue;
        }

        private string GetStringParameter(System.Collections.Generic.Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (parameters != null && parameters.ContainsKey(key))
            {
                object value = parameters[key];
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }
    }
}

