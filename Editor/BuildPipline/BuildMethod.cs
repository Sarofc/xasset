using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Saro.XAsset
{
    public class BuildMethod
    {
        public int order;
        public string description;
        public bool required;
        public bool selected = false;
        public Func<bool> callback;

        private static List<BuildMethod> s_BuildMethods;
        public static List<BuildMethod> BuildMethods
        {
            get
            {
                if (s_BuildMethods == null)
                {
                    s_BuildMethods = GetBuildMethods();
                }
                return s_BuildMethods;
            }
        }

        private static List<BuildMethod> GetBuildMethods()
        {
            var ret = new List<BuildMethod>();
            var assembly = Assembly.Load("Saro.XAsset.Editor");
            var type = assembly.GetType("Saro.XAsset.BuildMethods");
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<BuildMethodAttribute>();
                if (attr != null)
                {
                    var buildMethod = new BuildMethod()
                    {
                        order = attr.order,
                        description = attr.description,
                        required = attr.required,
                        selected = attr.required,
                        callback = () =>
                        {
                            if (method.ReturnType == typeof(bool))
                            {
                                return (bool)method.Invoke(null, null);
                            }
                            else
                            {
                                try { method.Invoke(null, null); }
                                catch (Exception e)
                                {
                                    UnityEngine.Debug.LogException(e);
                                    return false;
                                }
                                return true;
                            }
                        }
                    };
                    ret.Add(buildMethod);
                }
            }

            ret.Sort((a, b) => a.order.CompareTo(b.order));

            return ret;
        }
    }

    public class BuildMethodAttribute : Attribute
    {
        public int order;
        public string description;
        public bool required;

        public BuildMethodAttribute(int order, string description, bool required = true)
        {
            this.order = order;
            this.description = description;
            this.required = required;
        }
    }
}
