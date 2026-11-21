using UnityEngine;

namespace PhoneUI
{
    static class PhoneUiSimpleIcons
    {
        static Sprite _logout;

        public static Sprite GetLogoutIcon()
        {
            if (_logout != null)
                return _logout;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

            var gold = PhoneUiTheme.TextGold;
            var black = PhoneUiTheme.TextBlack;
            DrawFilledRect(tex, 10, 12, 44, 40, gold);
            DrawFilledRect(tex, 14, 16, 36, 32, black);
            DrawFilledRect(tex, 30, 24, 20, 4, gold);
            DrawFilledRect(tex, 42, 20, 4, 12, gold);
            DrawFilledRect(tex, 38, 18, 12, 4, gold);

            tex.Apply();
            _logout = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _logout;
        }

        static void DrawFilledRect(Texture2D tex, int x, int y, int w, int h, Color c)
        {
            for (int py = y; py < y + h; py++)
            for (int px = x; px < x + w; px++)
            {
                if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                    tex.SetPixel(px, py, c);
            }
        }
    }
}
