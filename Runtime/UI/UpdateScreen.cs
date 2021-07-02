using UnityEngine;
using UnityEngine.UI;

namespace Saro.XAsset.Update
{
    public class UpdateScreen : MonoBehaviour, IUpdater
    {
        [SerializeField] private Text version;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text progressText;
        [SerializeField] private Button buttonStart;
        [SerializeField] private AssetUpdaterComponent resourceUpdater;


        private void Start()
        {
            version.text = "Ver 0.0.0.1";
            resourceUpdater.Listener = this;
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
            version.text = "Ver " + ver;
        }

        public void OnClear()
        {
            buttonStart.gameObject.SetActive(true);
        }

        #endregion
    }
}