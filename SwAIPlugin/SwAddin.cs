using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks.Interop.swconst;
using System.Windows.Forms; // Required for the Task Pane UI

namespace SwAIPlugin
{
    [ComVisible(true)]
    [Guid("5936C588-77EC-4925-A234-36A03D199AE9")]
    [ProgId("SwAIPlugin.SwAddin")]
    public class SwAddin : ISwAddin
    {
        public SldWorks swApp;
        public int swCookie;
        public CommandManager iCmdMgr;

        // --- NEW: Task Pane Variables ---
        public TaskpaneView taskPaneView;
        public TaskPaneUI taskPaneControl;

        // Constants for our button
        public const int MAIN_CMD_GROUP_ID = 5;
        public const int MAIN_ITEM_ID = 0;
        public const int TRAINING_ITEM_ID = 1;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            swApp = (SldWorks)ThisSW;
            swCookie = Cookie;

            // 1. Setup the callback
            bool result = swApp.SetAddinCallbackInfo(0, this, swCookie);

            // 2. Build the Ribbon Button
            this.AddCommandMgr();

            // 3. Build the Task Pane (Side Panel) -- NEW STEP
            this.AddTaskPane();

            return true;
        }

        public bool DisconnectFromSW()
        {
            this.RemoveTaskPane(); // Clean up UI first
            this.RemoveCommandMgr();
            swApp = null;
            return true;
        }

        // ==========================================
        //  TASK PANE LOGIC (NEW SECTION)
        // ==========================================
        public void AddTaskPane()
        {
            string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string iconPath = Path.Combine(dllPath, "icon.png");

            // Create the Task Pane in SolidWorks
            taskPaneView = swApp.CreateTaskpaneView2(iconPath, "AI Assistant");

            // Load your UserControl into the Task Pane
            // valid progId or empty string for .NET controls
            taskPaneControl = (TaskPaneUI)taskPaneView.AddControl("SwAIPlugin.TaskPaneUI", "");
            
            // Pass SolidWorks application reference to the UI
            if (taskPaneControl != null)
            {
                taskPaneControl.SetSolidWorksApp(swApp);
            }
        }

        public void RemoveTaskPane()
        {
            if (taskPaneView != null)
            {
                taskPaneView.DeleteView();
                Marshal.ReleaseComObject(taskPaneView);
                taskPaneView = null;
            }
        }

        // ==========================================
        //  COMMAND MANAGER (BUTTONS)
        // ==========================================
        public void AddCommandMgr()
        {
            iCmdMgr = swApp.GetCommandManager(swCookie);

            string title = "AI Assistant";
            string toolTip = "Ask the AI";
            string hint = "Opens the AI Chat Window";

            int errors = 0;
            CommandGroup cmdGroup = iCmdMgr.CreateCommandGroup2(
                MAIN_CMD_GROUP_ID,
                title,
                toolTip,
                hint,
                -1,
                true,
                ref errors
            );

            if (errors != 0)
            {
                // Group might already exist, handle if necessary
            }

            // Set the Icon
            string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string iconPath = Path.Combine(dllPath, "icon.png");

            if (File.Exists(iconPath))
            {
                cmdGroup.LargeIconList = iconPath;
                cmdGroup.SmallIconList = iconPath;
                cmdGroup.LargeMainIcon = iconPath;
                cmdGroup.SmallMainIcon = iconPath;
            }

            // Add the button
            int menuToolbarOption = (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem;
            int cmdIndex = cmdGroup.AddCommandItem2("Launch AI", -1, hint, toolTip, 0, "EnableAI", "EnableUI", MAIN_ITEM_ID, menuToolbarOption);

            // Add Training Data Generation button
            int trainingCmdIndex = cmdGroup.AddCommandItem2(
                "Generate Training Data",
                -1,
                "Generate training data from parts in Training_Input_Parts folder",
                "Training Data Generator",
                0,
                "GenerateTrainingData",
                "EnableTrainingUI",
                TRAINING_ITEM_ID,
                menuToolbarOption
            );

            cmdGroup.HasToolbar = true;
            cmdGroup.HasMenu = true;
            cmdGroup.Activate();
        }

        public void RemoveCommandMgr()
        {
            if (iCmdMgr != null)
            {
                iCmdMgr.RemoveCommandGroup(MAIN_CMD_GROUP_ID);
            }
        }

        // ==========================================
        // UI CALLBACKS
        // ==========================================
        public void EnableAI()
        {
            // NEW LOGIC: Instead of a popup, show the Task Pane
            if (taskPaneView != null)
            {
                taskPaneView.ShowView();
            }
            else
            {
                swApp.SendMsgToUser("Error: Task Pane not loaded.");
            }
        }

        public int EnableUI()
        {
            return 1; // 1 = Enabled
        }

        /// <summary>
        /// Callback for the Generate Training Data button
        /// </summary>
        public void GenerateTrainingData()
        {
            try
            {
                // Get the DLL location to find the relative paths
                string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                
                // Navigate up from bin/Debug to the solution root, then to the folders
                // DLL is at: SwAIPlugin/bin/Debug/SwAIPlugin.dll
                // Folders are at: Training_Input_Parts and Training_Output_Data
                string solutionRoot = Path.GetFullPath(Path.Combine(dllPath, "..", "..", ".."));
                
                string inputFolder = Path.Combine(solutionRoot, "Training_Input_Parts");
                string outputFolder = Path.Combine(solutionRoot, "Training_Output_Data");

                // Ensure folders exist
                if (!Directory.Exists(inputFolder))
                {
                    Directory.CreateDirectory(inputFolder);
                    swApp.SendMsgToUser($"Created input folder:\n{inputFolder}\n\nPlease add .SLDPRT files and run again.");
                    return;
                }

                // Check if there are any parts to process
                string[] partFiles = Directory.GetFiles(inputFolder, "*.SLDPRT", SearchOption.TopDirectoryOnly);
                if (partFiles.Length == 0)
                {
                    partFiles = Directory.GetFiles(inputFolder, "*.sldprt", SearchOption.TopDirectoryOnly);
                }

                if (partFiles.Length == 0)
                {
                    swApp.SendMsgToUser($"No .SLDPRT files found in:\n{inputFolder}\n\nPlease add SolidWorks part files and run again.");
                    return;
                }

                // Confirm with user
                int response = swApp.SendMsgToUser2(
                    $"Training Data Generator\n\n" +
                    $"Found {partFiles.Length} part file(s) to process.\n\n" +
                    $"Input: {inputFolder}\n" +
                    $"Output: {outputFolder}\n\n" +
                    $"This will open each part, step through features,\nand capture screenshots + metadata.\n\n" +
                    $"Continue?",
                    (int)swMessageBoxIcon_e.swMbQuestion,
                    (int)swMessageBoxBtn_e.swMbYesNo
                );

                if (response != (int)swMessageBoxResult_e.swMbHitYes)
                {
                    return;
                }

                // Create and run the FeatureWalker
                FeatureWalker walker = new FeatureWalker(swApp, inputFolder, outputFolder);
                string log = walker.ProcessFolder();

                // Save the log
                string logPath = Path.Combine(outputFolder, "walker_log.txt");
                File.WriteAllText(logPath, log);

                // Show completion message
                swApp.SendMsgToUser2(
                    $"Training data generation complete!\n\n" +
                    $"Output saved to:\n{outputFolder}\n\n" +
                    $"Log saved to:\n{logPath}\n\n" +
                    $"Next step: Run the Python script to generate train.jsonl",
                    (int)swMessageBoxIcon_e.swMbInformation,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
            catch (Exception ex)
            {
                swApp.SendMsgToUser($"Error generating training data:\n{ex.Message}");
            }
        }

        public int EnableTrainingUI()
        {
            return 1; // 1 = Enabled
        }

        // ==========================================
        // COM REGISTRATION
        // ==========================================
        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                Microsoft.Win32.RegistryKey addinkey = hklm.CreateSubKey(keyname);
                addinkey.SetValue(null, 0);
                addinkey.SetValue("Description", "AI Assistant for SolidWorks");
                addinkey.SetValue("Title", "SwAIPlugin");

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                addinkey = hkcu.CreateSubKey(keyname);
                addinkey.SetValue(null, 1);
            }
            catch (System.Exception e)
            {
                Console.WriteLine("Error registering DLL: " + e.Message);
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                hklm.DeleteSubKey(keyname);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                hkcu.DeleteSubKey(keyname);
            }
            catch (System.Exception e)
            {
                Console.WriteLine("Error unregistering DLL: " + e.Message);
            }
        }
    }
}