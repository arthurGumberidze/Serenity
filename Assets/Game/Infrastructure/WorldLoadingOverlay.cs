using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure
{
    public sealed class WorldLoadingOverlay : MonoBehaviour
    {
        private string stage = "Создаём новый мир…";
        private bool failed;
        public float Progress { get; private set; }
        public void SetProgress(float value) => Progress = Mathf.Max(Progress, Mathf.Clamp01(value));
        private GUIStyle titleStyle, stageStyle;
        public void SetStage(string value)
        {
            // Weighted completed stages, not a timer pretending to measure work.
            if(value.StartsWith("Подготавливаем рельеф"))SetProgress(.03f);
            else if(value.StartsWith("Подготавливаем обзор"))SetProgress(.12f);
            else if(value.StartsWith("Создаём ближайший"))SetProgress(.25f);
            else if(value.StartsWith("Подготавливаем маршруты"))SetProgress(.35f);
            else if(value.StartsWith("Заселяем мир"))SetProgress(.45f);
            else if(value.StartsWith("Заселяем семьи"))SetProgress(.50f);
            else if(value.StartsWith("Создаём жителей"))SetProgress(.55f);
            else if(value.StartsWith("Расселяем животных"))SetProgress(.60f);
            else if(value.StartsWith("Подключаем жителей"))SetProgress(.70f);
            else if(value.StartsWith("Подготавливаем береговые"))SetProgress(.75f);
            stage = value;
            Debug.Log("WM15_BOOT " + value);
        }
        public void Fail() { failed = true; stage = "Не удалось создать мир. Подробности записаны в журнал игры."; }
        private void OnGUI()
        {
            GUI.depth = -10000;
            var previous = GUI.color;
            GUI.color = new Color(.055f, .075f, .055f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            stageStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.Label(new Rect(30, Screen.height * .4f, Screen.width - 60, 60), failed ? "Ошибка загрузки" : "Загрузка", titleStyle);
            GUI.Label(new Rect(30, Screen.height * .4f + 70, Screen.width - 60, 80), stage, stageStyle);
            var bar = new Rect(Screen.width * .2f, Screen.height * .4f + 160, Screen.width * .6f, 24);
            GUI.color = new Color(.2f,.24f,.2f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(.78f,.63f,.36f);
            GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Progress,bar.height),Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x,bar.y+28,bar.width,32), Mathf.FloorToInt(Progress*100)+"%", stageStyle);
            if (failed && GUI.Button(new Rect(Screen.width * .5f - 120, Screen.height * .4f + 240, 240, 45), "Главное меню"))
                SceneManager.LoadScene("MainMenu");
            GUI.color = previous;
        }
    }
}
