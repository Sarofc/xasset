using MGF;
using UnityEngine;

namespace Saro.XAsset.Update
{
    public interface INetworkMonitorListener
    {
        void OnReachablityChanged(NetworkReachability reachability);
    }

    [ObjectSystem]
    public class NetworkMonitorComponentAwakeSystem : AwakeSystem<NetworkMonitorComponent>
    {
        public override void Awake(NetworkMonitorComponent self)
        {
            self.Awake();
        }
    }

    [ObjectSystem]
    public class NetworkMonitorComponentUpdateSystem : UpdateSystem<NetworkMonitorComponent>
    {
        public override void Update(NetworkMonitorComponent self)
        {
            self.Update();
        }
    }

    [ObjectSystem]
    public class NetworkMonitorComponentDestroySystem : DestroySystem<NetworkMonitorComponent>
    {
        public override void Destroy(NetworkMonitorComponent self)
        {
            self.Stop();
        }
    }

    public class NetworkMonitorComponent : Entity
    {
        private NetworkReachability m_Reachability;
        public INetworkMonitorListener Listener { get; set; }
        [SerializeField] private float SampleTime = 0.5f;
        private float m_Time;
        private bool m_Started;

        public void Awake()
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

        public void Update()
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