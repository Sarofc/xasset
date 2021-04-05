using UnityEngine;

namespace Saro.XAsset.Update
{
    public interface INetworkMonitorListener
    {
        void OnReachablityChanged(NetworkReachability reachability);
    }

    public class NetworkMonitor : MonoBehaviour
    {
        private NetworkReachability m_Reachability;
        public INetworkMonitorListener Listener { get; set; }
        [SerializeField] private float SampleTime = 0.5f;
        private float m_Time;
        private bool m_Started;

        private void Start()
        {
            m_Reachability = Application.internetReachability;
            Restart();
        }

        public void Restart()
        {
            m_Time = Time.timeSinceLevelLoad;
            m_Started = true;
        }

        public void Stop()
        {
            m_Started = false;
        }

        private void Update()
        {
            if (m_Started && Time.timeSinceLevelLoad - m_Time >= SampleTime)
            {
                var state = Application.internetReachability;
                if (m_Reachability != state)
                {
                    if (Listener != null)
                    {
                        Listener.OnReachablityChanged(state);
                    }
                    m_Reachability = state;
                }
                m_Time = Time.timeSinceLevelLoad;
            }
        }
    }
}