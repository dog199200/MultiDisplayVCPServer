using System.Diagnostics;
using Microsoft.Win32;

namespace MultiDisplayVCPServer
{
    public partial class SplashForm : Form
    {
        public SplashForm()
        {
            Debug.WriteLine("SplashForm() constructor started.");
            InitializeComponent();
            Debug.WriteLine("SplashForm() constructor finished.");
            // ... (rest of constructor) ...
        }

        // --- ADD THIS 'LOAD' EVENT HANDLER ---
        private void SplashForm_Load(object? sender, EventArgs e)
        {
            Debug.WriteLine("SplashForm_Load() started.");
            // 1. Subscribe to the "server ready" signal
            Program.ServerStateChanged += OnServerStateChanged;
            Debug.WriteLine("Subscribed to Program.ServerStateChanged.");

            // 2. Start the server initialization
            Program.StartServerLoop();
            Debug.WriteLine("SplashForm_Load() finished, server loop started.");
        }

        // --- ADD THIS 'SERVER STATE' HANDLER ---
        private void OnServerStateChanged(object? sender, int state)
        {
            Debug.WriteLine($"OnServerStateChanged() called. New state: {state}");
            // We only care about the "Running" state
            if (state == 1) // State 1 = "Running"
            {
                Debug.WriteLine("Server state is 1 (Running).");
                // Unsubscribe from the event
                Program.ServerStateChanged -= OnServerStateChanged;
                Debug.WriteLine("Unsubscribed from Program.ServerStateChanged.");

                // We need to launch the MainForm on a new UI thread
                // to replace this one.
                Debug.WriteLine("Creating new UI thread for MainForm.");
                var mainThread = new Thread(RunMainForm);
                mainThread.SetApartmentState(ApartmentState.STA);
                mainThread.Start();
                Debug.WriteLine("MainForm thread started.");

                // Close this splash screen (on its own UI thread)
                Debug.WriteLine("Invoking Close() on SplashForm.");
                this.Invoke(() => this.Close());
            }
        }

        // --- ADD THIS HELPER METHOD ---
        private void RunMainForm()
        {
            Debug.WriteLine("RunMainForm() started on new thread.");
            // This runs on the new thread, creating a new message loop
            // for the main application.
            Application.Run(new MainForm());
            Debug.WriteLine("RunMainForm() finished (MainForm was closed).");
        }

        private void lblStatus_Click(object? sender, EventArgs e)
        {
            // This was empty, but we'll leave it
            Debug.WriteLine("lblStatus_Click() called (no action).");
        }
    }
}