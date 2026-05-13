using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextManager : MonoBehaviour
{
    public TMP_Text _text;
    // Start is called before the first frame update
    void Start()
    {
        _text.text = "Waiting for response...";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void UpdateText(string newText)
    {
        _text.text = newText;
    }
}
