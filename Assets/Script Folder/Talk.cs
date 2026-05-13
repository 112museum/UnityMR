using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ButtonClickHandler : MonoBehaviour
{
    // Reference to the Animator component
    public Animator animator;

    public UnityEvent OnReplyRecieved;

    // The value to set the parameter to
    public bool parameterValue;

    void Start()
    {
        // Get the button component
        Button button = GetComponent<Button>();

        // Add a listener for when the button is clicked
        button.onClick.AddListener(OnClick);
    }

    // Method to handle button click
    void OnClick()
    {
        // Set the animator parameter
        OnReplyRecieved.Invoke();
    }
}