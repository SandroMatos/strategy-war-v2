using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastWars.Client.Editor
{
    public static class FrontierEditor
    {
        const string ScenePath = "Assets/LastWars/Scenes/Frontier.unity";
        [InitializeOnLoadMethod]
        static void Initialize() => EditorApplication.delayCall += FirstOpen;

        static void FirstOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(ScenePath)) return;
            try { ValidateContracts(); }
            catch (Exception error) { Debug.LogError("Frontier validation: " + error.Message); return; }
            const string key = "LastWars.Frontend.InitialOpen.v1";
            if (EditorPrefs.GetBool(Application.dataPath + key, false)) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                { Debug.Log("Frontier pronto. Cena atual tem alterações não salvas: use LastWars > Abrir frontend quando desejar."); return; }
            EditorSceneManager.OpenScene(ScenePath);
            EditorPrefs.SetBool(Application.dataPath + key, true);
        }
        [MenuItem("LastWars/Abrir frontend")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("LastWars/Validar contratos do frontend")]
        public static void ValidateContracts()
        {
            var production = JsonUtility.FromJson<ProductionDto>("{\"collection_target\":105,\"production_per_hour\":{\"food\":3482},\"stored_resources\":{\"food\":50},\"local_capacity\":1000}");
            Require(production.collection_target == 105 && production.production_per_hour.food == 3482, "Contrato de produção variável inválido");
            GridPlacementRulesTests.Run();
            ProductionRulesTests.Run();
            const string id = "4c523111-602f-456c-8000-a234bd842baa";
            var data = JsonUtility.FromJson<BaseDto>("{\"player_id\":\"" + id + "\",\"resources\":{\"food\":13,\"iron\":2147483648,\"oil\":7,\"gold\":0},\"buildings\":[{\"id\":\"" + id + "\",\"type\":\"iron_mine\",\"width\":2,\"height\":2,\"construction_finish_time\":null}],\"grid_bounds\":{\"min_x\":6,\"max_x\":16,\"min_y\":6,\"max_y\":16}}");
            Require(Contracts.ValidBase(data, id), "Base válida rejeitada");
            Require(!Contracts.ValidBase(data, "other"), "Base de outro jogador aceita");
            Require(data.resources.iron == 2147483648L && data.resources.food == 13, "Recursos truncados");
            Require(string.IsNullOrEmpty(data.buildings[0].construction_finish_time), "Timestamp null inválido");
            Require(!data.resources.Covers(new ResourcesDto { food = 14 }), "Recursos insuficientes aceitos");
            var upgrade = JsonUtility.FromJson<UpgradeDto>("{\"required_resources\":{\"iron\":12},\"stat_effects\":{\"power\":{\"current\":5,\"next\":9,\"difference\":4}},\"unlocked_features\":{\"unlocks\":[\"test\"]}}");
            Require(upgrade.stat_effects.power.next == 9 && upgrade.required_resources.iron == 12 && upgrade.unlocked_features.unlocks.Length == 1, "Contrato de evolução inválido");
            var obstacle = JsonUtility.FromJson<ObstacleList>("{\"items\":[{\"id\":\"test\",\"is_cleared\":true}]}");
            Require(obstacle.items.Length == 1 && obstacle.items[0].is_cleared, "Lista de obstáculos inválida");
            Require(!Contracts.ValidSession(new SessionDto()), "Sessão incompleta aceita");
            Require(Resources.Load<ClientConfiguration>("FrontierConfiguration") != null, "Configuração ausente");
            File.WriteAllText("Library/FrontierValidation.txt", DateTime.UtcNow.ToString("O") + "\nPASS: contracts, resource precision, null dates, identity, upgrade, obstacles, configuration\n");
            Debug.Log("Frontier: contratos e configuração validados.");
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
