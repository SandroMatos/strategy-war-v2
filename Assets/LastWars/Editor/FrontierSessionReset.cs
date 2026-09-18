using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastWars.Client.Editor
{
    public static class FrontierSessionReset
    {
        [Serializable] sealed class Request { public string player_id; public string api_base_url; }
        [MenuItem("LastWars/Aplicar reset solicitado de sessão")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode antes do reset.");
            const string path = "Library/FrontierSessionResetRequest.json";
            if (!File.Exists(path)) { Debug.Log("Nenhum reset de sessão solicitado."); return; }
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            var config = Resources.Load<ClientConfiguration>("FrontierConfiguration");
            if (request == null || !Guid.TryParse(request.player_id, out _) || config == null || config.apiBaseUrl.TrimEnd('/') != request.api_base_url)
                throw new InvalidOperationException("Reset não corresponde ao jogador/servidor esperado.");
            string key = "LastWars.Client.Session." + request.api_base_url;
            string saved = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(saved))
            {
                var session = JsonUtility.FromJson<SessionDto>(saved);
                if (session?.player?.id != request.player_id)
                    throw new InvalidOperationException("A sessão atual pertence a outro jogador; preservada.");
                PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
            }
            File.Delete(path);
            File.WriteAllText("Library/FrontierSessionResetResult.txt", "PASS: sessão da conta removida ausente. Próximo Play Mode abre o cadastro.");
            Debug.Log("Sessão da conta removida limpa. O próximo Play Mode abrirá o cadastro.");
        }
    }
}
