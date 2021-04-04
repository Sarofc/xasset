using UnityEngine;
using UnityEngine.UI;

namespace Saro.XAsset
{
    public class UpdateScreen : MonoBehaviour, Update.IUpdater
    {
        public Text version;
        public Slider progressBar;
        public Text progressText;
        public Button buttonStart;

        private void Start()
        {
            version.text = "APP: 4.0\nRES：1";
            var updater = GetComponent<Updater>();
            updater.listener = this;
        }

        #region IUpdater implementation

        public void OnStart()
        {
            buttonStart.gameObject.SetActive(false);
        }

        public void OnMessage(string msg)
        {
            progressText.text = msg;
        }

        public void OnProgress(float progress)
        {
            progressBar.value = progress;
        }

        public void OnVersion(string ver)
        {
            version.text = "APP: 4.0\nRES: " + ver;
        }


        public void OnClear()
        {
            buttonStart.gameObject.SetActive(true);
        }

        #endregion
    }
}