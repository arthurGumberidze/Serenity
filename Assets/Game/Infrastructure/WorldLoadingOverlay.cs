using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure
{
    public sealed class WorldLoadingOverlay : MonoBehaviour
    {
        private string stage = "Создаём новый мир…";
        private bool failed;
        private GUIStyle titleStyle, stageStyle;
        public void SetStage(string value)
        {
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
            if (failed && GUI.Button(new Rect(Screen.width * .5f - 120, Screen.height * .4f + 170, 240, 45), "Главное меню"))
                SceneManager.LoadScene("MainMenu");
            GUI.color = previous;
        }
    }
}
