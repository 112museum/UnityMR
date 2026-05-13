using UnityEngine;
using TMPro; // Import TextMeshPro namespace
using MixedReality.Toolkit.UX;

public class SliderValueDisplay : MonoBehaviour
{
    [SerializeField]
    private MixedReality.Toolkit.UX.Slider slider; // Reference to the MRTK Slider component
    [SerializeField]
    private TextMeshProUGUI valueText; // Reference to the TextMeshProUGUI component

    private void Awake()
    {
        if (slider == null)
        {
            Debug.LogError("Slider reference is not set.");
            return;
        }

        if (valueText == null)
        {
            Debug.LogError("Text reference is not set.");
            return;
        }

        // Subscribe to the slider's OnValueUpdated event
        slider.OnValueUpdated.AddListener(OnSliderValueUpdated);
    }

    private void OnDestroy()
    {
        if (slider != null)
        {
            // Unsubscribe from the slider's OnValueUpdated event
            slider.OnValueUpdated.RemoveListener(OnSliderValueUpdated);
        }
    }

    // Event handler for when the slider value is updated
    private void OnSliderValueUpdated(MixedReality.Toolkit.UX.SliderEventData eventData)
    {
        valueText.text = eventData.NewValue.ToString("F2"); // Display the new value with 2 decimal places
    }
}
