using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using HoloToolkit.Unity;
using Adept;

#if WINDOWS_UWP
using Microsoft.Tools.WindowsDevicePortal;
using System.Threading.Tasks;
using Windows.Security.Credentials;
#endif

public enum FBControllerState
{
    /// <summary>
    /// The controller is still initializing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Attempting silent authentication
    /// </summary>
    SilentAuth,

    /// <summary>
    /// Attempting interactive login.
    /// </summary>
    LoggingIn,
    
    /// <summary>
    /// The user is logged out.
    /// </summary>
    LoggedOut,

    /// <summary>
    /// The user is logged in.
    /// </summary>
    LoggedIn
};

public class FBController : MonoBehaviour
{
    #region Constants
    private const bool IS_SIDELOADED = true;                // Whether this is a side loaded or store build
    static public readonly string PortalResourceName = "Device Portal";   // Resource name used to store the password in password vault
    private const float UPDATE_PERIOD = 5.0f;               // How often the update loop polls the portal for IPD updates
    #endregion // Constants

    #region Inspector Fields
    [Tooltip("Text control used to display error messages.")]
    public Text errorText;

    [Tooltip("Text control used to display the current IPD.")]
    public Text ipdText;

    [Tooltip("GameObject that contains all elements for the Logged In state.")]
    public GameObject loggedInState;

    [Tooltip("Manager used for speaking text.")]
    public TextToSpeechManager textToSpeech;
    #endregion // Inspector Fields

    #region Member Variables
    private AppViewInfo authView;
    private KeywordRecognizer keywordRecognizer;
    private Dictionary<string, float> keywords = new Dictionary<string, float>();
    private FBControllerState state;

    #if WINDOWS_UWP
    private DevicePortal portal;
    private PasswordVault vault;
    #endif
    #endregion // Member Variables

    #region Internal Methods
    #if WINDOWS_UWP
    /// <summary>
    /// The Authentication State Machine
    /// </summary>
    private async void AuthStateMachine()
    {
        switch (state)
        {
            case FBControllerState.Initializing:
                // Now doing silent auth
                State = FBControllerState.SilentAuth;

                // Attempt to authenticate in case already cached credentials
                if (await AuthSilentAsync())
                {
                    State = FBControllerState.LoggedIn;
                }
                else
                {
                    // Not authenticated. Start login flow.
                    State = FBControllerState.LoggingIn;
                    await StartLoginAsync();
                }
                break;

            case FBControllerState.LoggingIn:
                // Just returned from login dialog. Attempt silent auth again.
                if (await AuthSilentAsync())
                {
                    State = FBControllerState.LoggedIn;
                }
                else
                {
                    State = FBControllerState.LoggedOut;
                }
                break;
        }

        // If we get here and we're logged in, start the update loop
        if (state == FBControllerState.LoggedIn)
        {
            StartCoroutine(UpdateLoop());
        }
    }

    private async Task<bool> AuthSilentAsync()
    {
        // Placeholder
        PasswordCredential cred = null;

        // Try to get user name and password
        try
        {
            cred = vault.FindAllByResource(PortalResourceName).FirstOrDefault();

            // Password does not come across with the method above. We must call another method.
            if (cred != null)
            {
                cred = vault.Retrieve(PortalResourceName, cred.UserName);
            }
        }
        catch { }

        // If no credentials were found, fail
        if (cred == null)
        {
            return false;
        }

        // Credentials found. Try and log into portal
        try
        {
            // Create portal object
            portal = new DevicePortal(
                new DefaultDevicePortalConnection(
                    "https://127.0.0.1",
                    cred.UserName,
                    cred.Password));

            // Attempt to connect
            await portal.Connect();

            // Get IPD
            var ipd = await portal.GetInterPupilaryDistance();

            // Success!
            return true;
        }
        catch (Exception ex)
        {
            // Problem
            ShowError(ex.Message);
            return false;
        }
    }

    private async void ReadValues()
    {
        try
        {
            // Get IPD
            float ipd = await portal.GetInterPupilaryDistance();

            // Show IPD
            ShowIpd(ipd);
        }
        catch (Exception ex)
        {
            // Show error on Unity thread
            ShowError(ex.Message);
        }
    }
    #endif

    private void RegisterVoiceCommands()
    {
       
        try
        {
            // Add all supported IPDs
            for (int i = 0; i < 25; i++)
            {
                keywords.Add(string.Format("Set IPD to {0}", 50 + i), 50 + i);
            }

            // Tell the KeywordRecognizer about our keywords.
            keywordRecognizer = new KeywordRecognizer(keywords.Keys.ToArray());

            // Register a callback for the KeywordRecognizer and start recognizing!
            keywordRecognizer.OnPhraseRecognized += KeywordRecognizer_OnPhraseRecognized;
            keywordRecognizer.Start();
        }
        catch {}
    }

    #if WINDOWS_UWP
    /// <summary>
    /// Starts the login (authentication) process by switching to XAML.
    /// </summary>
    private async Task StartLoginAsync()
    {
        // Get auth view if not already obtained
        if (authView == null)
        {
            authView = AppViewManager.Views["Auth"];
            authView.Consolidated += AuthView_Consolidated;
        }

        // Switch to auth view
        await authView.SwitchAsync();
    }

    private async void UpdateIpd(float ipd)
    {
        try
        {
             // Set IPD
            await portal.SetInterPupilaryDistance(ipd);

            // Reread values
            ReadValues();

            // Define message to speak
            var speakText = string.Format("IPD set to {0}", ipd);

            // Speak the message
            textToSpeech.SpeakText(speakText);
        }
        catch (Exception ex)
        {
            // Show error on Unity thread
            ShowError(ex.Message);
        }
    }

    #else
    private void AuthenticateAsync() { }
    private void ReadValues() { }
    private void UpdateIpd(float ipd)   {  }
    #endif

    /// <summary>
    /// Shows the error state.
    /// </summary>
    /// <param name="error">
    /// The error message to display.
    /// </param>
    private void ShowError(string error)
    {
        ThreadExtensions.RunOnAppThread(() =>
        {
            errorText.text = error;
        });
    }

    /// <summary>
    /// Shows the specified IPD.
    /// </summary>
    /// <param name="ipd">
    /// The IPD to show.
    /// </param>
    private void ShowIpd(float ipd)
    {
        ThreadExtensions.RunOnAppThread(() =>
        {
            ipdText.text = ipd.ToString();
        });
    }

    /// <summary>
    /// Loop that polls device portal and updates the UI.
    /// </summary>
    private IEnumerator UpdateLoop()
    {
        while (true)
        {
            // Actually read IPD
            ReadValues();

            // Wait for next poll
            yield return new WaitForSeconds(UPDATE_PERIOD);
        }
    }

    private void UpdateVisualState()
    {
        ThreadExtensions.RunOnAppThread(() =>
        {
            switch (state)
            {
                case FBControllerState.LoggedIn:
                    loggedInState.SetActive(true);
                    break;
                default:
                    loggedInState.SetActive(false);
                    break;
            }
        });
    }
    #endregion // Internal Methods

    #region Behaviour Overrides
    void Start()
    {
        if (IS_SIDELOADED)
        {
            // Register voice commands
            RegisterVoiceCommands();

            #if WINDOWS_UWP
            // Create the valut
            vault = new PasswordVault();

            // Start authentication state machine
            AuthStateMachine();
            #endif
        }
        else
        {
            // Store builds cannot log in
            State = FBControllerState.LoggedOut;
        }
    }

    void OnDestroy()
    {
    }
    #endregion // Behaviour Overrides

    #region Overrides / Event Handlers
    private void AuthView_Consolidated(object sender, EventArgs e)
    {
       #if WINDOWS_UWP
        // Auth view has been closed. Continue auth state machine.
        AuthStateMachine();
        #endif
    }

    private void KeywordRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        float ipd;
        if (keywords.TryGetValue(args.text, out ipd))
        {
            UpdateIpd(ipd);
        }
    }
    #endregion // Overrides / Event Handlers

    #region Public Properties
    /// <summary>
    /// Gets the current state of the controller.
    /// </summary>
    public FBControllerState State
    {
        get
        {
            return state;
        }
        set
        {
            if (state != value)
            {
                state = value;
                UpdateVisualState();
            }
        }
    }
    #endregion // Public Properties
}
