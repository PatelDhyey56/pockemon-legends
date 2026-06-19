#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoardUIGenerator : EditorWindow
{
    [MenuItem("Pokemon/Generate Static Board UI")]
    public static void GenerateUI()
    {
        BoardInputHandler handler = Object.FindFirstObjectByType<BoardInputHandler>();
        if (handler == null)
        {
            Debug.LogError("Could not find BoardInputHandler in the scene! Please open the battle scene and make sure the script is on a GameObject.");
            return;
        }

        SerializedObject so = new SerializedObject(handler);
        Transform boardParent = so.FindProperty("boardParent").objectReferenceValue as Transform;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        if (boardParent == null)
        {
            GameObject bp = new GameObject("BoardParent", typeof(RectTransform));
            bp.transform.SetParent(canvas.transform, false);
            boardParent = bp.transform;
            so.FindProperty("boardParent").objectReferenceValue = boardParent;
        }

        // Generate Global Background (Full screen)
        Transform globalBg = canvas.transform.Find("GlobalBackground");
        if (globalBg == null)
        {
            GameObject globalBgGo = new GameObject("GlobalBackground", typeof(RectTransform), typeof(Image));
            globalBgGo.transform.SetParent(canvas.transform, false); 
            globalBgGo.transform.SetAsFirstSibling(); // Push it to the very back
            RectTransform globalBgRt = globalBgGo.GetComponent<RectTransform>();
            globalBgRt.anchorMin = Vector2.zero;
            globalBgRt.anchorMax = Vector2.one;
            globalBgRt.sizeDelta = Vector2.zero;
            Image globalBgImg = globalBgGo.GetComponent<Image>();
            
            // Get the image from the BoardInputHandler
            Sprite bgSprite = so.FindProperty("backgroundImage").objectReferenceValue as Sprite;
            if (bgSprite != null) {
                globalBgImg.sprite = bgSprite;
            }
            
            globalBgImg.preserveAspect = false;
            globalBgImg.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        // Generate Board Background (Since we deleted CreateBackground)
        Transform existingBg = canvas.transform.Find("BoardBackground");
        if (existingBg == null)
        {
            GameObject boardBg = new GameObject("BoardBackground", typeof(RectTransform), typeof(Image));
            boardBg.transform.SetParent(canvas.transform, false);
            int bpIndex = boardParent.GetSiblingIndex();
            boardBg.transform.SetSiblingIndex(bpIndex); // Put it right before the boardParent
            Image boardBgImg = boardBg.GetComponent<Image>();
            boardBgImg.color = new Color(0.05f, 0.05f, 0.05f, 0.7f);
            RectTransform bgRt = boardBg.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(850f, 850f);
            
            // Move board down slightly to make room for banner
            RectTransform bpRt = boardParent.GetComponent<RectTransform>();
            bpRt.anchoredPosition = new Vector2(0f, -50f);
            bgRt.anchoredPosition = bpRt.anchoredPosition;
        }

        // Generate PlayerUIPanel
        GameObject playerUIPanel = new GameObject("PlayerUIPanel", typeof(RectTransform));
        playerUIPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRt = playerUIPanel.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(840f, 300f);
        panelRt.anchoredPosition = new Vector2(0f, 680f); // Moved up to make room
        panelRt.localScale = new Vector3(1.15f, 1.15f, 1f);

        so.FindProperty("playerUIPanel").objectReferenceValue = panelRt;

        // Message Banner (Enhanced UX)
        GameObject bannerGo = new GameObject("MessageBanner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        bannerGo.transform.SetParent(canvas.transform, false);
        RectTransform bannerRt = bannerGo.GetComponent<RectTransform>();
        bannerRt.sizeDelta = new Vector2(550f, 75f);
        bannerRt.anchoredPosition = new Vector2(0f, 445f); // Centered perfectly between cards and board
        Image bannerImg = bannerGo.GetComponent<Image>();
        
        // Use Unity's default rounded background sprite for a pill-like shape
        Sprite roundedSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (roundedSprite != null) {
            bannerImg.sprite = roundedSprite;
            bannerImg.type = Image.Type.Sliced;
        }
        bannerImg.color = new Color(0.1f, 0.12f, 0.18f, 0.98f); // Sleek dark panel
        
        // Add subtle outline to banner
        Outline bOutline = bannerGo.AddComponent<Outline>();
        bOutline.effectColor = new Color(1f, 1f, 1f, 0.15f);
        bOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Message text inside banner
        GameObject msgGo = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgGo.transform.SetParent(bannerGo.transform, false);
        RectTransform msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.anchorMin = Vector2.zero;
        msgRt.anchorMax = Vector2.one;
        msgRt.sizeDelta = Vector2.zero;
        
        TextMeshProUGUI msgTxt = msgGo.GetComponent<TextMeshProUGUI>();
        msgTxt.alignment = TextAlignmentOptions.Center;
        msgTxt.fontSize = 32f;
        msgTxt.fontStyle = FontStyles.Bold;
        msgTxt.color = new Color(1f, 0.85f, 0.2f);
        
        // Shadow for text to pop out
        msgTxt.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        
        so.FindProperty("messageText").objectReferenceValue = msgTxt;

        // P1 Card
        CreateCard(so, playerUIPanel.transform, -220f, true);
        
        // P2 Card
        CreateCard(so, playerUIPanel.transform, 220f, false);

        so.ApplyModifiedProperties();
        Debug.Log("Successfully generated all deleted static UI elements and mapped them to BoardInputHandler!");
    }

    private static void CreateCard(SerializedObject so, Transform parent, float posX, bool isP1)
    {
        string pfx = isP1 ? "p1" : "p2";
        GameObject card = new GameObject(isP1 ? "P1Card" : "P2Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(400f, 280f);
        cardRt.anchoredPosition = new Vector2(posX, 0f);
        Image bgImg = card.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
        
        so.FindProperty(pfx + "PanelBg").objectReferenceValue = bgImg;

        // Inner border
        GameObject inner = new GameObject("InnerBorder", typeof(RectTransform), typeof(Image), typeof(Outline));
        inner.transform.SetParent(card.transform, false);
        inner.GetComponent<RectTransform>().sizeDelta = new Vector2(394f, 274f);
        inner.GetComponent<Image>().color = Color.clear;
        Outline outline = inner.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Name
        GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(card.transform, false);
        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(380f, 35f);
        nameRt.anchoredPosition = new Vector2(0f, 115f);
        TextMeshProUGUI nameTxt = nameGo.GetComponent<TextMeshProUGUI>();
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.fontSize = 20f;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = isP1 ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.7f, 0.5f);
        so.FindProperty(pfx + "NameText").objectReferenceValue = nameTxt;

        // Moves
        GameObject movesGo = new GameObject("MovesText", typeof(RectTransform), typeof(TextMeshProUGUI));
        movesGo.transform.SetParent(card.transform, false);
        RectTransform movesRt = movesGo.GetComponent<RectTransform>();
        movesRt.sizeDelta = new Vector2(80f, 20f);
        movesRt.anchoredPosition = new Vector2(155f, 115f);
        TextMeshProUGUI movesTxt = movesGo.GetComponent<TextMeshProUGUI>();
        movesTxt.alignment = TextAlignmentOptions.Right;
        movesTxt.fontSize = 11f;
        so.FindProperty(pfx + "MovesText").objectReferenceValue = movesTxt;

        // HP Background & Bars
        GameObject hpBgGo = new GameObject("HPBarBg", typeof(RectTransform), typeof(Image));
        hpBgGo.transform.SetParent(card.transform, false);
        RectTransform hpBgRt = hpBgGo.GetComponent<RectTransform>();
        hpBgRt.sizeDelta = new Vector2(360f, 20f);
        hpBgRt.anchoredPosition = new Vector2(0f, 85f);
        hpBgGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.9f);

        GameObject hpTrailGo = new GameObject("HPBarTrailing", typeof(RectTransform), typeof(Image));
        hpTrailGo.transform.SetParent(hpBgGo.transform, false);
        hpTrailGo.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 20f);
        Image trailImg = hpTrailGo.GetComponent<Image>();
        trailImg.type = Image.Type.Filled;
        trailImg.fillMethod = Image.FillMethod.Horizontal;
        trailImg.color = new Color(0.9f, 0.25f, 0.2f);
        so.FindProperty(pfx + "HpBarTrailing").objectReferenceValue = trailImg;

        GameObject hpFillGo = new GameObject("HPBarFill", typeof(RectTransform), typeof(Image));
        hpFillGo.transform.SetParent(hpBgGo.transform, false);
        hpFillGo.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 20f);
        Image fillImg = hpFillGo.GetComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.color = new Color(0.2f, 0.8f, 0.3f);
        so.FindProperty(pfx + "HpBar").objectReferenceValue = fillImg;

        GameObject hpTextGo = new GameObject("HPText", typeof(RectTransform), typeof(TextMeshProUGUI));
        hpTextGo.transform.SetParent(card.transform, false);
        RectTransform hpTextRt = hpTextGo.GetComponent<RectTransform>();
        hpTextRt.sizeDelta = new Vector2(360f, 20f);
        hpTextRt.anchoredPosition = new Vector2(0f, 87f);
        TextMeshProUGUI hpTxt = hpTextGo.GetComponent<TextMeshProUGUI>();
        hpTxt.alignment = TextAlignmentOptions.Center;
        hpTxt.fontSize = 12f;
        hpTxt.fontStyle = FontStyles.Bold;
        so.FindProperty(pfx + "HpText").objectReferenceValue = hpTxt;

        CreatePokemonUI(so, card.transform, -100f, -30f, pfx, 1);
        CreatePokemonUI(so, card.transform, 100f, -30f, pfx, 2);
    }

    private static void CreatePokemonUI(SerializedObject so, Transform parent, float posX, float posY, string playerPfx, int pokeIdx)
    {
        string pfx = playerPfx + "Poke" + pokeIdx;
        GameObject col = new GameObject("PokemonCol_" + pokeIdx, typeof(RectTransform));
        col.transform.SetParent(parent, false);
        RectTransform colRt = col.GetComponent<RectTransform>();
        colRt.sizeDelta = new Vector2(100f, 150f);
        colRt.anchoredPosition = new Vector2(posX, posY);

        GameObject avBg = new GameObject("AvatarBg", typeof(RectTransform), typeof(Image));
        avBg.transform.SetParent(col.transform, false);
        avBg.GetComponent<RectTransform>().sizeDelta = new Vector2(74f, 74f);
        avBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 35f);
        avBg.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.2f, 0.95f);

        GameObject av = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        av.transform.SetParent(col.transform, false);
        av.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 70f);
        av.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 35f);
        so.FindProperty(pfx + "Avatar").objectReferenceValue = av.GetComponent<Image>();

        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(col.transform, false);
        nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 15f);
        nameGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 70f);
        TextMeshProUGUI nameTxt = nameGo.GetComponent<TextMeshProUGUI>();
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.fontSize = 9f;
        nameTxt.fontStyle = FontStyles.Bold;
        so.FindProperty(pfx + "Name").objectReferenceValue = nameTxt;

        GameObject stats = new GameObject("Stats", typeof(RectTransform));
        stats.transform.SetParent(col.transform, false);
        stats.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 25f);
        stats.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -15f);

        GameObject stone = new GameObject("StoneImage", typeof(RectTransform), typeof(Image));
        stone.transform.SetParent(stats.transform, false);
        stone.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 24f);
        stone.GetComponent<RectTransform>().anchoredPosition = new Vector2(-15f, 0f);
        so.FindProperty(pfx + "Stone").objectReferenceValue = stone.GetComponent<Image>();

        GameObject energyTxt = new GameObject("EnergyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        energyTxt.transform.SetParent(stats.transform, false);
        energyTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 25f);
        energyTxt.GetComponent<RectTransform>().anchoredPosition = new Vector2(20f, 0f);
        TextMeshProUGUI eTxt = energyTxt.GetComponent<TextMeshProUGUI>();
        eTxt.alignment = TextAlignmentOptions.Left;
        eTxt.fontSize = 18f;
        eTxt.fontStyle = FontStyles.Bold;
        so.FindProperty(pfx + "EnergyText").objectReferenceValue = eTxt;

        GameObject energyBg = new GameObject("EnergyBg", typeof(RectTransform), typeof(Image));
        energyBg.transform.SetParent(col.transform, false);
        energyBg.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 12f);
        energyBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -40f);
        energyBg.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        so.FindProperty(pfx + "EnergyBgTransform").objectReferenceValue = energyBg.transform;

        GameObject energyFill = new GameObject("EnergyFill", typeof(RectTransform), typeof(Image));
        energyFill.transform.SetParent(energyBg.transform, false);
        energyFill.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 12f);
        Image eFillImg = energyFill.GetComponent<Image>();
        eFillImg.type = Image.Type.Filled;
        eFillImg.fillMethod = Image.FillMethod.Horizontal;
        eFillImg.color = Color.clear;
        so.FindProperty(pfx + "EnergyBar").objectReferenceValue = eFillImg;

        GameObject atkLabel = new GameObject("AttackLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        atkLabel.transform.SetParent(col.transform, false);
        atkLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 30f);
        atkLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -65f);
        TextMeshProUGUI aTxt = atkLabel.GetComponent<TextMeshProUGUI>();
        aTxt.alignment = TextAlignmentOptions.Center;
        aTxt.fontSize = 10f;
        so.FindProperty(pfx + "AttackLabel").objectReferenceValue = aTxt;
    }
}
#endif
