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