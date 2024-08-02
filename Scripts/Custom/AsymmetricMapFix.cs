using System.Collections;
using UnityEngine;
using Teal;
using Newtonsoft.Json;

/*
 * Workaround for asymmetric map spawns/flags - sets teams based on Z position.
 * author: Tomeno
 * script version: 1.0
 * target game version: 0.8.71a
 */

[DisallowMultipleComponent]
// ReSharper disable once CheckNamespace
public class AsymmetricMapFix : MonoBehaviour {
    [JsonProperty("BlueIsOffset")] public bool blueIsOffset = true; // objects with offset are Blue/Bravo team
    [JsonProperty("ModifyFlags")] public bool modifyFlags = true;
    [JsonProperty("ModifySpawns")] public bool modifySpawns = true;
    [JsonProperty("ResetZ")] public bool resetZ = true; // set position Z to zero after fixing team
    [JsonProperty("DisableOnMirror")] public bool disableOnMirror = true; // don't run the script for mirrored maps 
    [JsonProperty("EnableLog")] public bool enableLog = true; // enable printing 
    
    private void Start() {
        try
        {
            if (disableOnMirror && Map.Get.mirror)
            {
                if (enableLog)
                    Debug.Log("[AMF] Not fixing teams - map is mirrored");
                return;
            }

            if (enableLog)
                Debug.Log("[AMF] Running AsymmetricMapFix");
            if (modifyFlags)
            {
                foreach (BaseCapture bc in BaseCapture._bases)
                {
                    if (!bc)
                        continue;
                    FixBaseTeam(bc);
                }
            }

            if (modifySpawns)
            {
                foreach (Respawn res in Respawn._list)
                {
                    if (!res)
                        continue;
                    if (res.respawnPrefab != "Gostek")
                        continue;
                    FixRespawnTeam(res);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.Log($"[AMF.Start] @@@@@ ERROR @@@@@");
            try {
                Debug.Log($"[AMF.Start] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
                Debug.Log($"[AMF.Start] Exception message: " + ex.Message);
            } catch (System.Exception ex2) {
                Debug.Log($"[AMF.Start] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
            }
        }
    }
    
    private int GetZTeam(float z) {
        if (System.Math.Abs(z) > 0.001f)
            return blueIsOffset ? 0 : 1;
        else
            return blueIsOffset ? 1 : 0;
    }
    
    private void FixRespawnTeam(Respawn res) {
        int newTeamNum = GetZTeam(res.transform.position.z);
        if (enableLog)
            Debug.Log("[AMF] Modifying spawn at position " + res.transform.position + " from team " + res.team.Number + " to team " + newTeamNum);
        res.team.Number = newTeamNum;
        if (resetZ)
            res.transform.position = new Vector3(res.transform.position.x, res.transform.position.y, 0f);
    }
    
    private void FixBaseTeam(BaseCapture bc) {
        int newTeamNum = GetZTeam(bc.transform.position.z);
        if (enableLog)
            Debug.Log("[AMF] Modifying base at position " + bc.transform.position + " from team " + bc.team.Number + " to team " + newTeamNum);
        bc.team.Number = newTeamNum;
        if (resetZ)
            bc.transform.position = new Vector3(bc.transform.position.x, bc.transform.position.y, 0f);
    }
}
