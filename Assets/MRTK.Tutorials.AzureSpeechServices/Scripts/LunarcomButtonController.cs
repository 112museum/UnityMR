// Copyright (c) Microsoft Corporation. 
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MixedReality.Toolkit.UX;

public class LunarcomButtonController : MonoBehaviour
{
    [Header("Animator Reference")]
    public Animator sparrowAnimator;

    [Header("Reference Objects")]
    public RecognitionMode speechRecognitionMode = RecognitionMode.Disabled;

    [Space(6)]
    [Header("Button States")]
    public Sprite Default;
    public Sprite Activated;
    public PressableButton pressableButton;

    private Button button;
    private bool isSelected = false;

    private LunarcomController lunarcomController;

    private void Start()
    {
        sparrowAnimator = GameObject.Find("Canvas/SparrowButton/Sparrow").GetComponent<Animator>();



        lunarcomController = LunarcomController.lunarcomController;
        button = GetComponent<Button>();
    }

    public bool GetIsSelected()
    {
        return isSelected;
    }

    public void ToggleSelected()
    {
        if (isSelected)
        {
            DeselectButton();
            sparrowAnimator.SetBool("Connection", false); // Change the parameter value

        }
        else
        {
            button.image.sprite = Activated;
            isSelected = true;
            lunarcomController.SetActiveButton(GetComponent<LunarcomButtonController>());

            if (lunarcomController.IsOfflineMode())
            {
                lunarcomController.SelectMode(RecognitionMode.Offline);
            } else
            {
                lunarcomController.SelectMode(speechRecognitionMode);
                sparrowAnimator.SetBool("Connection", true); // Change the parameter value

            }
        }
    }

    public void ShowNotSelected()
    {
        button.image.sprite = Default;
        isSelected = false;
    }

    public void DeselectButton()
    {
        ShowNotSelected();
        lunarcomController.SelectMode(RecognitionMode.Disabled);
        pressableButton.ForceSetToggled(false);
    }
}
