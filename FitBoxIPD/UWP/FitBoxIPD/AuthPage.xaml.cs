using Adept;
using Microsoft.Tools.WindowsDevicePortal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UnityEngine;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace FitBoxIPD
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AuthPage : Page
    {
        #region Member Variables
        private DevicePortal portal;
        private PasswordVault vault;
        #endregion // Member Variables

        #region Constructors
        public AuthPage()
        {
            this.InitializeComponent();
            vault = new PasswordVault();
        }
        #endregion // Constructors

        #region Internal Methods
        private async Task DoLoginAsync()
        {
            // Attempt to authenticate
            if (await TryAuthenticateAsync())
            {
                // Save credentials
                StoreCredentials();

                // Return to main view
                await ReturnToMain(true);
            }
        }

        private async Task ReturnToMain(bool authenticated)
        {
            // Get our auth view
            var authView = AppViewManager.Views["Auth"];

            // Switch back to Main view and consolidate (close) this one
            await AppViewManager.Views["Main"].SwitchAsync(authView, ApplicationViewSwitchingOptions.ConsolidateViews);

            // Try to notify the controller but must do it on Unitys thread
            ThreadExtensions.RunOnAppThread(() =>
            {
                // Get our FB Controller
                var controller = GameObject.FindObjectOfType<FBController>();

                // If found, notify to continue
                if (controller != null)
                {
                    controller.ContinueAuthFromLogin((authenticated ? portal : null));
                }
            });
        }

        private async Task<bool> TryAuthenticateAsync()
        {
            try
            {
                // Create portal object
                portal = new DevicePortal(
                    new DefaultDevicePortalConnection(
                        "https://127.0.0.1",
                        UserNameBox.Text,
                        PasswordBox.Password));

                // Get cert (OK to use untrusted since it's loopback)
                await portal.GetRootDeviceCertificate(acceptUntrustedCerts: true);

                // Attempt to connect
                await portal.Connect();

                // Get IPD
                var ipd = await portal.GetInterPupilaryDistance();

                // Success!
                return true;
            }
            catch (Exception)
            {
                // Problem
                await new MessageDialog("Authentication was not successful. Please make sure Device Portal is enabled and check your password.", "Error").ShowAsync();
                return false;
            }
        }

        private void StoreCredentials()
        {
            // Placeholder
            PasswordCredential cred = null;

            // Could fail
            try
            {
                // If there is a credential stored, get it
                cred = vault.FindAllByResource(FBController.PortalResourceName).FirstOrDefault();
            }
            catch (Exception) { }

            // If there was an existing credential, remove it. Otherwise, create a new one.
            if (cred != null)
            {
                vault.Remove(cred);
            }
            else
            {
                cred = new PasswordCredential();
            }

            // Update
            cred.Resource = FBController.PortalResourceName;
            cred.UserName = UserNameBox.Text;
            cred.Password = PasswordBox.Password;

            // Save
            vault.Add(cred);
        }
        #endregion // Internal Methods

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await DoLoginAsync();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch back to Main view and consolidate (close) this one
            await ReturnToMain(false);
        }
    }
}
