
using System.Collections.Generic;
using UnityEngine;

namespace Saro.XAsset
{
    public class Reference
    {
        /// <summary>
        /// 引用计数
        /// </summary>
        public int RefCount => m_RefCount;

        /// <summary>
        /// 依赖的对象
        /// </summary>
        private List<Object> m_Requires;

        private int m_RefCount;

        public bool IsUnused()
        {
            if (m_Requires != null)
            {
                for (var i = 0; i < m_Requires.Count; i++)
                {
                    var item = m_Requires[i];
                    if (item != null)
                        continue;
                    Release();
                    m_Requires.RemoveAt(i);
                    i--;
                }
                if (m_Requires.Count == 0)
                    m_Requires = null;
            }
            return m_RefCount <= 0;
        }

        public void Retain()
        {
            m_RefCount++;
        }

        public void Release()
        {
            m_RefCount--;
        }

        public void Require(Object obj)
        {
            if (m_Requires == null)
                m_Requires = new List<Object>();

            m_Requires.Add(obj);
            Retain();
        }

        public void Dequire(Object obj)
        {
            if (m_Requires == null)
                return;

            if (m_Requires.Remove(obj))
                Release();
        }
    }
}
