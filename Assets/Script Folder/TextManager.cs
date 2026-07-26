using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextManager : MonoBehaviour
{
    public TMP_Text _text;

    // Player/NPC lines for the free-chat two-line display (UpdatePlayerLine/UpdateNpcLine).
    // Kept separate from UpdateText's single-line usage (scripted narration, "typing...")
    // so the two display modes don't clobber each other.
    private string playerLine;
    private string npcLine;

    // Start is called before the first frame update
    void Start()
    {
        _text.text = "Waiting for response...";
    }

    // Update is called once per frame
    void Update()
    {

    }

    // Replaces the whole panel with a single line — scripted story narration, typing
    // indicators, etc. Also clears any player/npc free-chat lines so a later free-chat
    // update starts from a clean slate instead of appending to stale text.
    public void UpdateText(string newText)
    {
        playerLine = null;
        npcLine = null;
        _text.text = newText;
    }

    // Free-chat display: shows the player's recognized speech and the NPC's reply as two
    // lines on the same panel. Call UpdatePlayerLine when the player's line changes and
    // UpdateNpcLine when the NPC's line changes; each only touches its own line.
    public void UpdatePlayerLine(string text)
    {
        playerLine = text;
        RenderChatLines();
    }

    public void UpdateNpcLine(string text)
    {
        npcLine = text;
        RenderChatLines();
    }

    private void RenderChatLines()
    {
        if (string.IsNullOrEmpty(playerLine)) _text.text = npcLine ?? "";
        else if (string.IsNullOrEmpty(npcLine)) _text.text = playerLine;
        else _text.text = playerLine + "\n" + npcLine;
    }
}
