using System;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwAIPlugin
{
    /// <summary>
    /// Analyzes SolidWorks models and extracts information for AI context
    /// </summary>
    public class ModelAnalyzer
    {
        private SldWorks swApp;

        public ModelAnalyzer(SldWorks app)
        {
            swApp = app;
        }

        /// <summary>
        /// Analyzes the active model and returns a JSON-like string description
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
                analysis.AppendLine();

                // Part-specific analysis
                if (swModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                {
                    PartDoc partDoc = swModel as PartDoc;
                    analysis.AppendLine(AnalyzePart(partDoc));
                }
                // Assembly-specific analysis
                else if (swModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    AssemblyDoc assyDoc = swModel as AssemblyDoc;
                    analysis.AppendLine(AnalyzeAssembly(assyDoc));
                }
                // Drawing-specific analysis
                else if (swModel.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    DrawingDoc drawDoc = swModel as DrawingDoc;
                    analysis.AppendLine("Drawing document detected.");
                }

                // Feature Information
                analysis.AppendLine();
                analysis.AppendLine("FEATURES:");
                analysis.AppendLine(GetFeatureTree(swModel));

                // Mass Properties (if available)
                analysis.AppendLine();
                analysis.AppendLine("MASS PROPERTIES:");
                analysis.AppendLine(GetMassProperties(swModel));

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

        private string AnalyzePart(PartDoc partDoc)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("PART ANALYSIS:");
            
            try
            {
                ModelDoc2 swModel = partDoc as ModelDoc2;
                
                // Get feature count
                FeatureManager featureMgr = swModel.FeatureManager;
                int featureCount = featureMgr.GetFeatureCount(false);
                info.AppendLine($"  Total Features: {featureCount}");

                // Get body count
                Body2[] bodies = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, false) as Body2[];
                if (bodies != null)
                {
                    info.AppendLine($"  Solid Bodies: {bodies.Length}");
                }

                // Get surface body count
                Body2[] surfaceBodies = partDoc.GetBodies2((int)swBodyType_e.swSheetBody, false) as Body2[];
                if (surfaceBodies != null)
                {
                    info.AppendLine($"  Surface Bodies: {surfaceBodies.Length}");
                }

                // Check if part has configurations
                ConfigurationManager configMgr = swModel.ConfigurationManager;
                Configuration activeConfig = configMgr.ActiveConfiguration;
                if (activeConfig != null)
                {
                    info.AppendLine($"  Active Configuration: {activeConfig.Name}");
                }
            }
            catch (Exception ex)
            {
                info.AppendLine($"  Error: {ex.Message}");
            }

            return info.ToString();
        }

        private string AnalyzeAssembly(AssemblyDoc assyDoc)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("ASSEMBLY ANALYSIS:");
            
            try
            {
                ModelDoc2 swModel = assyDoc as ModelDoc2;
                
                // Get component count
                object[] components = assyDoc.GetComponents(false) as object[];
                if (components != null)
                {
                    info.AppendLine($"  Total Components: {components.Length}");
                }

                // Get feature count
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

        private string GetFeatureTree(ModelDoc2 swModel)
        {
            StringBuilder features = new StringBuilder();
            
            try
            {
                FeatureManager featureMgr = swModel.FeatureManager;
                int featureCount = featureMgr.GetFeatureCount(false);
                
                if (featureCount > 0)
                {
                    features.AppendLine($"  Total Features: {featureCount}");
                    features.AppendLine("  (Detailed feature list requires version-specific API)");
                    features.AppendLine("  Features are available in the model but detailed iteration");
                    features.AppendLine("  will be implemented with the correct API methods.");
                }
                else
                {
                    features.AppendLine("  No features found.");
                }
            }
            catch (Exception ex)
            {
                features.AppendLine($"  Error reading features: {ex.Message}");
            }

            return features.ToString();
        }

        private string GetMassProperties(ModelDoc2 swModel)
        {
            StringBuilder props = new StringBuilder();
            
            // Note: Mass properties API varies by SolidWorks version
            // For now, we'll skip this to avoid API compatibility issues
            // This can be implemented later with version-specific code
            
            props.AppendLine("  Mass properties: Not implemented in this version");
            props.AppendLine("  (Will be added with version-specific API calls)");
            
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
                    
                    Body2[] bodies = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, false) as Body2[];
                    int bodyCount = bodies != null ? bodies.Length : 0;
                    
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

