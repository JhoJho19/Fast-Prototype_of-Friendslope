using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractionHintPresenter : MonoBehaviour
{
    private readonly Dictionary<Object, HintRequest> activeHints = new();
    private readonly List<Object> staleOwners = new();

    private TMP_Text textHint;

    private struct HintRequest
    {
        public string Text;
        public int Priority;
        public int Frame;
    }

    private void Awake()
    {
        CacheReferences();
        HideHint();
    }

    private void OnEnable()
    {
        CacheReferences();
        HideHint();
    }

    private void LateUpdate()
    {
        PresentCurrentHint();
    }

    public void SubmitHint(Object owner, string text, int priority)
    {
        if (owner == null ||
            string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        activeHints[owner] = new HintRequest
        {
            Text = text,
            Priority = priority,
            Frame = Time.frameCount
        };
    }

    private void CacheReferences()
    {
        if (textHint != null)
        {
            return;
        }

        Transform root = transform.root;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text candidate in texts)
        {
            if (candidate != null &&
                candidate.gameObject.name == "TextHint")
            {
                textHint = candidate;
                return;
            }
        }

        if (texts.Length > 0)
        {
            textHint = texts[0];
        }
    }

    private void PresentCurrentHint()
    {
        if (textHint == null)
        {
            return;
        }

        staleOwners.Clear();

        string nextText = null;
        int nextPriority = int.MinValue;

        foreach (KeyValuePair<Object, HintRequest> entry in activeHints)
        {
            if (entry.Key == null ||
                entry.Value.Frame != Time.frameCount)
            {
                staleOwners.Add(entry.Key);
                continue;
            }

            if (entry.Value.Priority < nextPriority)
            {
                continue;
            }

            nextPriority = entry.Value.Priority;
            nextText = entry.Value.Text;
        }

        foreach (Object owner in staleOwners)
        {
            activeHints.Remove(owner);
        }

        if (string.IsNullOrWhiteSpace(nextText))
        {
            HideHint();
            return;
        }

        if (textHint.text != nextText)
        {
            textHint.text = nextText;
        }

        if (!textHint.gameObject.activeSelf)
        {
            textHint.gameObject.SetActive(true);
        }
    }

    private void HideHint()
    {
        if (textHint != null &&
            textHint.gameObject.activeSelf)
        {
            textHint.gameObject.SetActive(false);
        }
    }
}
