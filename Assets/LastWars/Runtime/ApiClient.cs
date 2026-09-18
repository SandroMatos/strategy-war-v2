using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LastWars.Client
{
    public sealed class ApiClient : IDisposable
    {
        readonly HashSet<UnityWebRequest> requests = new HashSet<UnityWebRequest>();
        public string BaseUrl { get; }
        public string Token { get; set; }
        public ApiClient(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https")) throw new ArgumentException("Endereço de API inválido.");
            BaseUrl = baseUrl.TrimEnd('/');
        }
        public IEnumerator Send(string method, string path, string json, Action<string> success, Action<string> failure)
        {
            using (var request = new UnityWebRequest(BaseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 15;
                request.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrEmpty(Token)) request.SetRequestHeader("Authorization", "Bearer " + Token);
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                requests.Add(request);
                try
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) failure(Error(request));
                    else success(request.downloadHandler.text);
                }
                finally { requests.Remove(request); }
            }
        }
        public IEnumerator Get<T>(string path, Action<T> success, Action<string> failure) where T : class
        {
            yield return Send("GET", path, null, json =>
            {
                T value;
                try { value = JsonUtility.FromJson<T>(json); }
                catch (Exception) { failure("Resposta do servidor em formato inesperado."); return; }
                if (value == null) failure("O servidor retornou uma resposta vazia.");
                else success(value);
            }, failure);
        }
        static string Error(UnityWebRequest request)
        {
            if (request.responseCode == 0) return "Servidor indisponível ou tempo esgotado. Verifique a conexão e tente novamente.";
            if (request.responseCode == 401) return "Sessão inválida ou expirada. Sua conta foi preservada; nenhuma conta nova foi criada.";
            if (request.responseCode == 403) return "Você não tem permissão para esta ação.";
            if (request.responseCode == 409) return "Esse nome já está em uso ou a operação já foi realizada.";
            if (request.responseCode == 422) return "Os dados não atendem às regras do servidor. Confira os campos.";
            if (request.responseCode >= 500) return "O servidor não conseguiu concluir a operação. Atualize a base antes de tentar novamente.";
            try
            {
                var error = JsonUtility.FromJson<ApiErrorDto>(request.downloadHandler.text);
                if (!string.IsNullOrEmpty(error?.detail)) return "HTTP " + request.responseCode + ": " + error.detail;
            }
            catch (Exception) { /* FastAPI detail can also be an array. */ }
            return "Falha HTTP " + request.responseCode + ".";
        }
        public void Dispose()
        {
            foreach (var request in requests) { request.Abort(); request.Dispose(); }
            requests.Clear();
        }
    }

}
