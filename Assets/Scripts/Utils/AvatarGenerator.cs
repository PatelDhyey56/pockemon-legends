using System.Collections.Generic;
using UnityEngine;

public static class AvatarGenerator
{
    private static readonly Dictionary<string, string> CreatureImageMap = new Dictionary<string, string>
    {
        { "Ember Dragon", "pyro-draco" },
        { "Lava Hound", "canis-molten" },
        { "Tide Serpent", "serpens-aquatica" },
        { "Thorn Wolf", "lupus-spinosus" },
        { "Thunder Roc", "aquila-fulgur" },
        { "Astral Fox", "vulpes-astris" },
        { "Void Raven", "corvus-voidus" },
        { "Light Phoenix", "phonex" },
        { "Ancient Treant", "treabtius" },
        { "Coral Guardian", "coral-guardian" },
        { "Celestial Unicorn", "celestial-unicorn" },
        { "Storm Drake", "storm-drake" }
    };
    public static Sprite CreateAvatar(Color baseColor, Color eyeColor)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - size / 2f;
                float dy = y - size / 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float radius = size / 2.2f;

                Color c = Color.clear;
                if (dist < radius)
                {
                    // Draw outer border outline
                    if (dist > radius - 6f)
                    {
                        c = new Color(0.1f, 0.1f, 0.15f, 1f);
                    }
                    else
                    {
                        // Body gradient
                        float t = (dy + radius) / (radius * 2f);
                        c = Color.Lerp(baseColor * 0.7f, baseColor, t);

                        // Draw belly highlight
                        float bellyDist = Mathf.Sqrt(dx * dx + (dy + 15f) * (dy + 15f));
                        if (bellyDist < radius / 2f)
                        {
                            c = Color.Lerp(c, Color.white, 0.8f);
                        }

                        // Eyes
                        float eyeY = 15f;
                        float eyeX = 22f;
                        float distEyeL = Mathf.Sqrt((dx + eyeX) * (dx + eyeX) + (dy - eyeY) * (dy - eyeY));
                        float distEyeR = Mathf.Sqrt((dx - eyeX) * (dx - eyeX) + (dy - eyeY) * (dy - eyeY));

                        if (distEyeL < 10f || distEyeR < 10f)
                        {
                            c = Color.white;
                            float pupilL = Mathf.Sqrt((dx + eyeX - 2f) * (dx + eyeX - 2f) + (dy - eyeY - 2f) * (dy - eyeY - 2f));
                            float pupilR = Mathf.Sqrt((dx - eyeX + 2f) * (dx - eyeX + 2f) + (dy - eyeY - 2f) * (dy - eyeY - 2f));
                            if (pupilL < 5f || pupilR < 5f)
                            {
                                c = eyeColor;
                            }
                        }
                    }
                }
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public static Sprite CreateCreatureSprite(string name)
    {
        if (name != null && CreatureImageMap.TryGetValue(name, out string fileName))
        {
            Sprite customSprite = Resources.Load<Sprite>("Creatures/" + fileName);
            if (customSprite != null)
            {
                return customSprite;
            }
        }

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        Color bodyColor = Color.gray;
        Color detailColor = Color.white;
        Color eyeColor = Color.black;

        if (name == "Ember Dragon") { bodyColor = new Color(1f, 0.5f, 0.1f); detailColor = new Color(1f, 0.8f, 0.2f); eyeColor = Color.cyan; }
        else if (name == "Lava Hound") { bodyColor = new Color(0.9f, 0.25f, 0.05f); detailColor = new Color(0.15f, 0.15f, 0.15f); eyeColor = Color.yellow; }
        else if (name == "Tide Serpent") { bodyColor = new Color(0.1f, 0.55f, 0.85f); detailColor = new Color(0.05f, 0.3f, 0.6f); eyeColor = Color.red; }
        else if (name == "Coral Guardian") { bodyColor = new Color(0.1f, 0.75f, 0.7f); detailColor = new Color(1f, 0.5f, 0.5f); eyeColor = Color.white; }
        else if (name == "Thorn Wolf") { bodyColor = new Color(0.1f, 0.5f, 0.2f); detailColor = new Color(0.4f, 0.8f, 0.3f); eyeColor = Color.red; }
        else if (name == "Ancient Treant") { bodyColor = new Color(0.45f, 0.28f, 0.1f); detailColor = new Color(0.2f, 0.7f, 0.3f); eyeColor = Color.yellow; }
        else if (name == "Thunder Roc") { bodyColor = new Color(1f, 0.85f, 0.1f); detailColor = new Color(0.2f, 0.2f, 0.2f); eyeColor = Color.black; }
        else if (name == "Storm Drake") { bodyColor = new Color(0.1f, 0.2f, 0.5f); detailColor = new Color(1f, 0.8f, 0.1f); eyeColor = Color.cyan; }
        else if (name == "Astral Fox") { bodyColor = new Color(0.6f, 0.2f, 0.85f); detailColor = new Color(0.8f, 0.4f, 0.95f); eyeColor = Color.white; }
        else if (name == "Void Raven") { bodyColor = new Color(0.1f, 0.05f, 0.2f); detailColor = new Color(0.7f, 0.15f, 0.85f); eyeColor = new Color(0.8f, 0.3f, 1f); }
        else if (name == "Celestial Unicorn") { bodyColor = new Color(0.95f, 0.95f, 1f); detailColor = new Color(0.9f, 0.75f, 0.2f); eyeColor = new Color(0.9f, 0.75f, 0.2f); }
        else if (name == "Light Phoenix") { bodyColor = new Color(1f, 0.7f, 0.1f); detailColor = new Color(0.9f, 0.15f, 0.1f); eyeColor = new Color(1f, 0.5f, 0f); }

        float centerX = size / 2f;
        float centerY = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float bodyRadius = size / 3.5f;
                if (dist < bodyRadius)
                {
                    Color c = bodyColor;
                    float t = (dy + bodyRadius) / (bodyRadius * 2f);
                    c = Color.Lerp(bodyColor * 0.75f, bodyColor, t);

                    if (name == "Thorn Wolf")
                    {
                        float bulbDist = Mathf.Sqrt(dx * dx + (dy - 20f) * (dy - 20f));
                        if (bulbDist < bodyRadius * 0.6f) c = detailColor;
                    }
                    else if (name == "Ember Dragon" || name == "Tide Serpent")
                    {
                        float bellyDist = Mathf.Sqrt(dx * dx + (dy + 10f) * (dy + 10f));
                        if (bellyDist < bodyRadius * 0.6f) c = detailColor;
                    }
                    else if (name == "Thunder Roc")
                    {
                        float cheekL = Mathf.Sqrt((dx + 18f) * (dx + 18f) + (dy + 8f) * (dy + 8f));
                        float cheekR = Mathf.Sqrt((dx - 18f) * (dx - 18f) + (dy + 8f) * (dy + 8f));
                        if (cheekL < 6f || cheekR < 6f) c = detailColor;
                    }

                    // Eyes
                    float eyeY = 10f;
                    float eyeX = 14f;
                    float distEyeL = Mathf.Sqrt((dx + eyeX) * (dx + eyeX) + (dy - eyeY) * (dy - eyeY));
                    float distEyeR = Mathf.Sqrt((dx - eyeX) * (dx - eyeX) + (dy - eyeY) * (dy - eyeY));
                    if (distEyeL < 6f || distEyeR < 6f)
                    {
                        c = Color.white;
                        float pupilL = Mathf.Sqrt((dx + eyeX - 1f) * (dx + eyeX - 1f) + (dy - eyeY - 1f) * (dy - eyeY - 1f));
                        float pupilR = Mathf.Sqrt((dx - eyeX + 1f) * (dx - eyeX + 1f) + (dy - eyeY - 1f) * (dy - eyeY - 1f));
                        if (pupilL < 3f || pupilR < 3f) c = eyeColor;
                    }

                    tex.SetPixel(x, y, c);
                }

                if (name == "Void Raven")
                {
                    if (dist >= bodyRadius && dist < bodyRadius * 1.5f)
                    {
                        float noise = Mathf.Sin(dx * 0.2f) * Mathf.Cos(dy * 0.2f) * 5f;
                        if (dist < bodyRadius * 1.3f + noise)
                        {
                            tex.SetPixel(x, y, new Color(detailColor.r, detailColor.g, detailColor.b, 0.4f));
                        }
                    }
                }

                if (name == "Ancient Treant" && dy > 18f && Mathf.Abs(dx) < 30f)
                {
                    float leafDist = Mathf.Sqrt(dx * dx + (dy - 35f) * (dy - 35f));
                    if (leafDist < 20f)
                    {
                        tex.SetPixel(x, y, detailColor);
                    }
                }
                else if (name == "Thunder Roc" && dy > 15f && Mathf.Abs(dx) > 15f && Mathf.Abs(dx) < 32f)
                {
                    float earL = Mathf.Sqrt((dx + 25f) * (dx + 25f) + (dy - 35f) * (dy - 35f));
                    float earR = Mathf.Sqrt((dx - 25f) * (dx - 25f) + (dy - 35f) * (dy - 35f));
                    if (earL < 8f || earR < 8f)
                    {
                        tex.SetPixel(x, y, bodyColor);
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
