using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;

#if WINDOWS_UWP
using System.Threading.Tasks;
using Microsoft.Tools.WindowsDevicePortal;

#endif

public class FBController : MonoBehaviour
{
    #region Constants
    // How often the update loop polls the portal for IPD updates
    private const float UPDATE_PERIOD = 5.0f;

    private DictationRecognizer m_DictationRecognizer;
    #endregion // Constants

    #region Inspector Fields
    [Tooltip("GameObject that contains all elements for the Error state.")]
    public GameObject errorState;

    [Tooltip("Text control used to display the current IPD.")]
    public Text ipdText;

    [Tooltip("GameObject that contains all elements for the Loaded state.")]
    public GameObject loadedState;

    [Tooltip("Text control used to display status messages.")]
    public Text statusText;

    [Tooltip("GameObject that contains all elements for the Updating state.")]
    public GameObject updatingState;   

    [Tooltip("Text control used to display the last dictated sentence")]
    public Text dictatedSentence;

    #endregion // Inspector Fields
    

    #region Member Variables
#if WINDOWS_UWP
    private DevicePortal portal;
#endif
    #endregion // Member Variables

    #region Internal Methods
#if WINDOWS_UWP
    private async Task EnsurePortalAsync()
    {
        if (portal == null)
        {
            portal = new DevicePortal(
                new DefaultDevicePortalConnection(
                    "https://10.0.0.15",
                    "???",
                    "???"));

            await portal.Connect();
        }
    }

    private async void ReadValues()
    {
        try
        {
            // Wait for connection
            await EnsurePortalAsync();

            // Get IPD
            float ipd = await portal.GetInterPupilaryDistance();

            // Show IPD on Unity thread
            UnityEngine.WSA.Application.InvokeOnAppThread(() => { ShowIpd(ipd); }, false);
        }
        catch
        {
            // Show error on Unity thread
            UnityEngine.WSA.Application.InvokeOnAppThread(ShowError, false);
        }
    }

    private async void WriteValues(float ipd)
    {
        try
        {
            // Wait for connection
            await EnsurePortalAsync();

             // Set IPD
            await portal.SetInterPupilaryDistance(ipd);            
            
        }
        catch
        {
            // Show error on Unity thread
            UnityEngine.WSA.Application.InvokeOnAppThread(ShowError, false);
        }
    }
    
#else
    private void ReadValues() { }
    private void WriteValues(float ipd)   {  }
#endif

        /// <summary>
        /// Shows the error state.
        /// </summary>
    private void ShowError()
    {
        // Set proper state
        errorState.SetActive(true);
        loadedState.SetActive(false);
        updatingState.SetActive(false);
    }

    /// <summary>
    /// Shows the specified IPD.
    /// </summary>
    /// <param name="ipd">
    /// The IPD to show.
    /// </param>
    private void ShowIpd(float ipd)
    {
        // Update text
        ipdText.text = ipd.ToString();

        // Set proper state
        errorState.SetActive(false);
        loadedState.SetActive(true);
        updatingState.SetActive(false);
    }

    /// <summary>
    /// Shows the update state.
    /// </summary>
    /// <param name="status">
    /// The status message text.
    /// </param>
    private void ShowUpdate(string status)
    {
        // Update text field
        statusText.text = status;

        // Set proper state
        errorState.SetActive(false);
        loadedState.SetActive(false);
        updatingState.SetActive(true);
    }

    /// <summary>
    /// Loop that polls device portal and updates the UI.
    /// </summary>
    private IEnumerator UpdateLoop()
    {
        while (true)
        {
            // Set update state            
            ShowUpdate("Reading IPD...");

            // Actually read IPD
            ReadValues();

            // Restart the dictationRecognizer in case of it had paused
            if (m_DictationRecognizer.Status != SpeechSystemStatus.Running)
            {
                m_DictationRecognizer.Start();
            }

            // Wait for next poll
            yield return new WaitForSeconds(UPDATE_PERIOD);
        }
    }
    #endregion // Internal Methods

    #region Behaviour Overrides

    // Use this for initialization
    void Start()
    {
        m_DictationRecognizer = new DictationRecognizer();

        m_DictationRecognizer.DictationResult += DictationRecognizer_DictationResult;

        m_DictationRecognizer.Start();
     
        StartCoroutine(UpdateLoop());
    }
    void OnDestroy()
    {
        m_DictationRecognizer.DictationResult -= DictationRecognizer_DictationResult;
        m_DictationRecognizer.Dispose();

    }
    #endregion // Behaviour Overrides

    #region Delegates
    private void DictationRecognizer_DictationResult(string text, ConfidenceLevel confidence)
    {
        if (dictatedSentence)
        {
            dictatedSentence.text = text;
        }

        if (text.ToUpper().Contains("SET") && text.ToUpper().Contains("IPD"))
        {
            string[] mWords = text.Split(' ');
            for (int i = 0; i< mWords.Length; i++)
            {
                try
                {
                    float mValue = float.Parse(mWords[i]);
                    if (mValue >= 55 && mValue <= 75)
                    {
                        WriteValues(mValue);
                    }
                    else
                    {
                        if (dictatedSentence)
                        {
                            dictatedSentence.text += "\n< < IPD must be between 55 - 75 > >";
                        }
                    }
                }
                catch 
                {

                }

            }
        }

    }
    #endregion //Delegates

}
