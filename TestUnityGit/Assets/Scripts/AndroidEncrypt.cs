using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using LitJson;

public class AndroidEncrypt : MonoBehaviour {
    enum MacState {
        Completed = 0,
        Install =1,
    }
    static string m_Path = Application.persistentDataPath + "/source.sl";
    static string m_Mac;
    static string m_Url = "http://czedu.caipow.cn:8082/api/user/getUserMAC?mac=";
    static string m_MACSaveUrl = "http://www.shenlin.xyz:8080/api/Mac";
    static string m_EncryptStr;

    static AndroidEncrypt m_Instance;
    public static AndroidEncrypt Instance {
        get {
            if (m_Instance == null) {
                m_Instance = new GameObject("AndroidEncrypt").AddComponent<AndroidEncrypt>();
                DontDestroyOnLoad(m_Instance.gameObject);
            }
            return m_Instance;
        }
    }

    [RuntimeInitializeOnLoadMethod]
    public static void Check() {
        // 获取机器码
#if UNITY_EDITOR
        m_Mac = "testPc";
#else
         m_Mac = SystemInfo.deviceUniqueIdentifier;
#endif
        m_EncryptStr = m_Mac + "ShenLin666" + Application.bundleIdentifier;

        //Debug.Log("m_Mac = " + m_Mac);
        if (File.Exists(m_Path)) {
            // 读到加密文本
            if (!Decode().Equals(m_EncryptStr, System.StringComparison.CurrentCulture)) {
                // 文本不对
                //Debug.Log("对比密钥不正确");
                QueryServer();
            } else {
                //Debug.Log("密钥正确 = " + m_EncryptStr);
            }
        } else {
            // 没存在
            QueryServer();
        }
    }

    // 加密并保存文本
    static bool EncryptAndSave() {
        //Debug.Log("加密并保存文本");
        byte[] buffer = Encoding.UTF8.GetBytes(m_EncryptStr);
        buffer = SecurityUtil.Xor(buffer);
        buffer = ZlibHelper.CompressBytes(buffer);

        //Debug.Log(m_Path);
        IOUtil.CreateTextFile(m_Path, buffer);
        return true;
    }

    static string Decode() {
        byte[] buffer = IOUtil.GetBuffer(m_Path);
        //Debug.Log(buffer.Length);
        buffer = ZlibHelper.DeCompressBytes(buffer);
        if (buffer == null)
            return string.Empty;

        buffer = SecurityUtil.Xor(buffer);
        string str = Encoding.UTF8.GetString(buffer);
        //Debug.Log(str);
        return str;
    }

    // 询问服务器
    static void QueryServer() {
        // 联网询问授权
        //Debug.Log("联网询问授权");
        Instance.StartCoroutine(ChecakMac());
    }

    static IEnumerator ChecakMac() {
        WWW www = new WWW(m_Url + m_Mac);
        yield return www;
        if (!string.IsNullOrEmpty(www.error)) {
            Quit();
            yield break;
        }

        //Debug.Log(www.text);
        JsonData jsondata = JsonMapper.ToObject(www.text);
        if (jsondata["Code"].ToString().Equals("faile",System.StringComparison.CurrentCultureIgnoreCase)) {
            // 未授权
            //Debug.Log(jsondata["msg"].ToString());
            yield return PostMac(m_Mac);
            Quit();
        } else {
            // 授权通过
            int ret = 0;
            string summary = jsondata["macInfo"]["Summary"].ToString();
            int.TryParse(summary, out ret);
            //Debug.Log(summary);
            if ((MacState)ret == MacState.Install) {
                // 机子是安装状态,可以获取秘钥
                EncryptAndSave();
            } else {
                //Debug.Log("机子是非安装状态");
                Quit();
            }
        }
    }

    /// <summary>
    /// 把未激活的mac推给服务器
    /// </summary>
    /// <param name="mac"></param>
    /// <returns></returns>
    static IEnumerator PostMac(string mac) {
        Dictionary<string, string> dic = new Dictionary<string, string>();
        dic.Add("MAC", mac);
        string jsonStr = JsonMapper.ToJson(dic);
        WWWForm form = new WWWForm();
        form.AddField("", jsonStr);

        WWW www = new WWW(m_MACSaveUrl, form);
        yield return www;
        if (www.error != null) {
            //Debug.Log(www.error);
        } else {
            //Debug.Log(www.text);
        }
    }

    static void Quit() {
        //Debug.Log("退出应用");

        InitCamera.QuitGame();
    }
}
