using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.IO;

public class TestMethods : MonoBehaviour {
    void Start() {
        var type = typeof(ScriptableRenderPass);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        var sw = new StringWriter();
        foreach (var m in methods) {
            if (m.Name == "Execute" || m.Name == "OnCameraSetup" || m.Name == "SetupRenderPasses") {
                sw.WriteLine(m.Name + "(");
                foreach (var p in m.GetParameters()) {
                    sw.WriteLine("  " + p.ParameterType.Name + " " + p.Name + (p.ParameterType.IsByRef ? " (ref/out)" : ""));
                }
                sw.WriteLine(")");
            }
        }
        File.WriteAllText("methods.txt", sw.ToString());
    }
}
