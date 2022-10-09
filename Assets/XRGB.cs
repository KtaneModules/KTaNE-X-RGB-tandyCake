using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class XRGB : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable[] buttons;
    public Sprite[] symbolSprites;
    public MeshRenderer screen;
    public MeshRenderer bg;

    private static readonly int[,] table = new int[4, 9]
    {
        { 2, 3, 1, 3, 2, 1, 0, 4, 0 },
        { 3, 1, 4, 2, 1, 2, 4, 0, 0 },
        { 4, 3, 0, 0, 3, 2, 1, 4, 1 },
        { 2, 3, 1, 0, 4, 3, 1, 2, 4 }
    };
    private static readonly string[] symbolIds = { "1 flipped", "7", "100", "107", "99", "2", "6", "62", "33",
                                                    "66", "55", "96", "21", "3 flipped", "69", "102", "0", "101",
                                                    "103", "23", "58", "4", "9", "104", "95", "18", "11" };
    private static readonly Color32[] allBlack = Enumerable.Repeat(new Color32(0, 0, 0, 255), 1000).ToArray();

    private Texture2D prevTexture;
    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private List<int> usedSymbols = new List<int>(4);
    private List<int> correspondingNumbers = new List<int>(4);
    private int _answerButton;

    private Color32[][] chosenSymbols = new Color32[4][];
    private List<Color32[]> displayedSymbol = new List<Color32[]>(1000);
    private int currentGenRowIx = 0;
    private int scannerIx = 0;
    private Coroutine scannerCoroutine;
    private bool topToBottom;
    private void Awake()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < 5; i++)
        {
            int ix = i;
            buttons[ix].OnInteract += () => { ButtonPress(ix); return false; };
        }
        Module.OnActivate += () => { scannerCoroutine = StartCoroutine(Scanner()); };
    }
    private void Start()
    {
        float hue = Rnd.Range(0f, 1);
        bg.material.color = Color.HSVToRGB(hue, 0.15f, 1f);
        for (int i = 0; i < 5; i++)
            buttons[i].GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(hue, 0.075f, 0.87f);
        GenerateStage();
    }
    private void GenerateStage()
    {
        topToBottom = Rnd.Range(0, 2) == 0;
        do
        {
            usedSymbols.Clear();
            correspondingNumbers.Clear();
            for (int i = 0; i < 4; i++)
            {
                int sym = Rnd.Range(0, 26);
                usedSymbols.Add(sym);
                correspondingNumbers.Add(table[i, sym % 9]);
            }
        } while (correspondingNumbers.Distinct().Count() != 4 || usedSymbols.Distinct().Count() != 4);
        _answerButton = Enumerable.Range(0, 5).Single(x => !correspondingNumbers.Contains(x));
        LogStuff();

        for (int i = 0; i < 4; i++)
            chosenSymbols[i] = symbolSprites[usedSymbols[i]].texture.GetPixels32();
        StartCoroutine(GenerateRows());
    }

    private void LogStuff()
    {
        Log("The symbols are being scanned from {0}.", topToBottom ? "top to bottom" : "bottom to top");
        Log("Displayed red channel shows [{0}], which corresponds to {1}.", symbolIds[usedSymbols[0]], correspondingNumbers[0] + 1);
        Log("Displayed green channel shows [{0}], which corresponds to {1}.", symbolIds[usedSymbols[1]], correspondingNumbers[1] + 1);
        Log("Displayed blue channel shows [{0}], which corresponds to {1}.", symbolIds[usedSymbols[2]], correspondingNumbers[2] + 1);
        Log("Displayed brightness channel shows [{0}], which corresponds to {1}.", symbolIds[usedSymbols[3]], correspondingNumbers[3] + 1);
        Log("Solution button is button {0}.", _answerButton + 1);
    }

    private void ButtonPress(int i)
    {
        buttons[i].AddInteractionPunch(1);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, buttons[i].transform);
        if (_moduleSolved)
            return;
        if (i == _answerButton)
            StartCoroutine(Solve(i));
        else Strike(i);
    }
    private void Strike(int btn)
    {
        Log("Pressed button {0}, strike! Module resetting...", btn + 1);
        Module.HandleStrike();
        scannerIx = 0;
        StopCoroutine(scannerCoroutine);
        SetScreen(allBlack);
        GenerateStage();
        scannerCoroutine = StartCoroutine(Scanner());
    }
    private IEnumerator Solve(int btn)
    {
        _moduleSolved = true;
        Log("Pressed button {0}, module solved.", btn + 1);
        Module.HandlePass();
        StopCoroutine(scannerCoroutine);
        SetScreen(allBlack);
        for (int i = 0; i < 4; i++)
        {
            Audio.PlaySoundAtTransform("Solve" + i, transform);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator Scanner()
    {
        //Log("Waiting...");
        yield return new WaitUntil(() => currentGenRowIx == 1000);
        //Log("Start scanning");
        float elapsed = 0;
        while (true)
        {
            while (scannerIx < 1000)
            {
                Color32[] row = new Color32[1000];
                int rowIx = topToBottom ? 999 - scannerIx : scannerIx;
                for (int i = 0; i < 1000; i++)
                    row[i] = displayedSymbol[rowIx][i];
                //Debug.Log(_displayedSymbol[scannerIx][500]);
                SetScreen(row);
                elapsed += Time.deltaTime;
                scannerIx = (int)(elapsed * 250);
                yield return null;
                //Debug.Log(scannerIx);
            }
            scannerIx = 0;
            SetScreen(allBlack);
            //Log("Scan finished in {0} seconds, restarting scan...", elapsed);
            elapsed = 0;
            yield return new WaitForSeconds(1);
        }
    }
    private void SetScreen(Color32[] colors)
    {
        Destroy(prevTexture);
        Texture2D newTxt = new Texture2D(1000, 1);
        newTxt.SetPixels32(colors, 0);
        newTxt.Apply();
        screen.material.mainTexture = newTxt;
        prevTexture = newTxt;
    }

    private IEnumerator GenerateRows()
    {
        currentGenRowIx = 0;
        displayedSymbol.Clear();
        while (currentGenRowIx < 1000)
        {
            for (int cyclesPerFrame = 0; cyclesPerFrame < 10; cyclesPerFrame++)
            {
                Color32[] row = new Color32[1000];
                for (int slot = 0; slot < 1000; slot++)
                {
                    int ix = 1000 * currentGenRowIx + slot;
                    int r = chosenSymbols[0][ix].a != 0 ? 127 : 0;
                    int g = chosenSymbols[1][ix].a != 0 ? 127 : 0;
                    int b = chosenSymbols[2][ix].a != 0 ? 127 : 0;

                    if (r == 0 && g == 0 && b == 0 && chosenSymbols[3][ix].a != 0)
                        row[slot] = new Color32(0x44, 0x44, 0x44, 0xFF);
                    else if (chosenSymbols[3][ix].a != 0)
                        row[slot] = new Color32((byte)(2 * r), (byte)(2 * g), (byte)(2 * b), 0xFF);
                    else
                        row[slot] = new Color32((byte)r, (byte)g, (byte)b, 0xFF);

                }
                displayedSymbol.Add(row);
                currentGenRowIx++;
            }
            yield return null;
        }
        //Log("Done generating rows");
    }
    private void Log(string msg, params object[] args)
    {
        Debug.LogFormat("[X-RGB #{0}] {1}", _moduleId, string.Format(msg, args));
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} press 3 [reading order] | !{0} press BL [buttons are TL, T, BL, B, BR]";
#pragma warning restore 414

    Dictionary<string, int> _twitchButtonMap = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase) 
    { { "tl", 1 }, { "t", 2 }, { "tm", 2 }, { "tc", 2 }, { "tr", 2 }, { "bl", 3 }, { "b", 4 }, { "bm", 4 }, { "bc", 4 }, { "br", 5 } };

    public KMSelectable[] ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, string.Format(@"^\s*(?:press )?({0})\s*$", _twitchButtonMap.Keys.Concat(_twitchButtonMap.Values.Select(v => v.ToString())).Join("|")), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        var buttonInput = m.Groups[1].Value;

        int buttonId;
        if ((int.TryParse(buttonInput, out buttonId) || _twitchButtonMap.TryGetValue(buttonInput, out buttonId)) && buttonId > 0 && buttonId <= buttons.Length)
            return new[] { buttons[buttonId - 1] };
        return null;
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        buttons[_answerButton].OnInteract();
        yield return new WaitForSeconds(.1f);
    }
}