using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LastWars.Client
{
    public sealed class FrontierApp : MonoBehaviour
    {
        ApiClient api;
        FrontierView ui;
        BaseWorld world;
        BuildingProductionView production;
        GridPlacementController placement;
        ObstacleDto[] placementObstacles;
        SessionDto session;
        BaseDto state;
        ClientConfiguration config;
        bool busy, lastRefreshSucceeded;
        string selected, sessionKey;
        float nextRefresh;

        void Start()
        {
            config = Resources.Load<ClientConfiguration>("FrontierConfiguration");

            if (config == null)
            {
                Debug.LogError(
                    "Frontier: verifique Assets/LastWars/Resources/FrontierConfiguration.asset."
                );

                enabled = false;
                return;
            }

            var worldObject = new GameObject("Isometric base");
            worldObject.transform.SetParent(transform);

            world = worldObject.AddComponent<BaseWorld>();
            world.Initialize();
            world.Selected = Select;

            var uiObject = new GameObject("Frontend UI", typeof(RectTransform));
            uiObject.transform.SetParent(transform);

            ui = uiObject.AddComponent<FrontierView>();
            ui.Initialize();
            production = uiObject.AddComponent<BuildingProductionView>();
            production.Initialize(world);
            placement = gameObject.AddComponent<GridPlacementController>();
            placement.Changed = ui.PlacementStatus;
            placement.Cancelled = () => { ui.HidePlacement(); ui.Status("Movimentação cancelada. Posição original preservada."); };
            placement.Confirmed = (id, x, y) => { ui.HidePlacement(); Run(MoveBuilding(id, x, y)); };

            ui.Refresh = () => Run(Refresh());

            ui.Recenter = () =>
            {
                if (state != null)
                {
                    world.Recenter(state);
                }
            };

            ui.ListBuildings = List;
            ui.Tutorial = () => Run(Tutorial());

            try
            {
                api = new ApiClient(config.apiBaseUrl);
            }
            catch (Exception exception)
            {
                var panel = ui.Dialog("Configuração inválida", false);
                FrontierView.Text(panel, exception.Message, 18, 100);
                return;
            }

            sessionKey = $"LastWars.Client.Session.{api.BaseUrl}";

            var savedSession = PlayerPrefs.GetString(sessionKey, string.Empty);

            if (!string.IsNullOrEmpty(savedSession))
            {
                Debug.Log("[AUTH] Sessão local encontrada. Tentando fazer login...");

                try
                {
                    session = JsonUtility.FromJson<SessionDto>(savedSession);
                }
                catch (Exception)
                {
                    Debug.LogError($"[AUTH] Erro ao ler sessão local:");
                    session = null;
                }

                if (!Contracts.ValidSession(session))
                {
                    Debug.LogWarning("[AUTH] Sessão local inválida.");
                    ui.Login(
                        api.BaseUrl,
                        "Sessão local ilegível. Preserve os dados e solicite recuperação da conta.",
                        true,
                        Create,
                        () => { }
                    );

                    return;
                }

                api.Token = session.access_token;

                Debug.Log("[AUTH] Sessão local válida.");
                Debug.Log($"[AUTH] User ID: {session.player.id}");
                Debug.Log($"[AUTH] Player: {session.player.display_name}");
                Debug.Log($"[AUTH] Access Token: {session.access_token}");

                Run(Connect());

                return;
            }

            ShowLogin(
                "Comandante, sua nova base está esperando. Escolha seu nome para começar."
            );
        }
        void ShowLogin(string message)
        {
            ui.Login(
                api.BaseUrl,
                message,
                session != null,
                Create,
                () => Run(Connect())
            );
        }


        void Create(string name)
        {
            name = name.Trim();

            if (!Regex.IsMatch(name, @"\A[A-Za-z0-9_ -]{3,32}\z"))
            {
                ShowLogin(
                    "Use 3 a 32 caracteres: letras sem acentos, números, espaços, _ ou -."
                );

                return;
            }

            Run(Register(name));
        }


        void Run(IEnumerator operation)
        {
            if (!busy && isActiveAndEnabled)
            {
                StartCoroutine(Execute(operation));
            }
        }


        IEnumerator Execute(IEnumerator operation)
        {
            busy = true;
            ui.Busy(true);

            try
            {
                yield return operation;
            }
            finally
            {
                busy = false;

                if (ui != null)
                {
                    ui.Busy(false);
                }

                nextRefresh =
                    Time.unscaledTime + Mathf.Max(5, config.refreshSeconds);
            }
        }

        IEnumerator Register(string name)
        {
            string failure = null;

            Debug.Log($"[AUTH] Tentando criar/login usuário: {name}");

            yield return api.Send(
                "POST",
                "/v1/auth/anonymous",
                JsonUtility.ToJson(
                    new AnonymousRequest
                    {
                        display_name = name
                    }
                ),
                json =>
                {
                    SessionDto result;

                    try
                    {
                        result = JsonUtility.FromJson<SessionDto>(json);
                    }
                    catch (Exception)
                    {
                        Debug.LogError(
                            $"[AUTH] Resposta de sessão inválida: Não repita a criação sem verificar o servidor."
                        );


                        failure =
                            "Resposta de sessão inválida. Não repita a criação sem verificar o servidor.";

                        return;
                    }

                    if (!Contracts.ValidSession(result))
                    {
                        Debug.LogError("[AUTH] Servidor retornou uma sessão inválida.");
                        failure = "O servidor não retornou uma sessão válida.";
                        return;
                    }

                    session = result;
                    api.Token = result.access_token;

                    // Development persistence only.
                    // Never log the token or silently replace this account.
                    PlayerPrefs.SetString(
                        sessionKey,
                        JsonUtility.ToJson(session)
                    );

                    PlayerPrefs.Save();
                },
                error => failure = error
            );

            if (failure != null)
            {
                ShowLogin(failure);
                yield break;
            }

            yield return Connect();
        }



        IEnumerator Connect()
        {
            ui.Login(api.BaseUrl, "Conectando à sua base...", true, Create, () => Run(Connect())); ui.Busy(true);
            yield return Refresh();
        }

        string PlayerPath => "/players/" + Uri.EscapeDataString(session.player.id);
        IEnumerator Refresh()
        {
            lastRefreshSucceeded = false;
            placementObstacles = null;

            if (session == null)
            {
                yield break;
            }

            string failure = null;
            BaseDto result = null;

            yield return api.Get<BaseDto>(
                PlayerPath + "/base",
                dto => result = dto,
                error => failure = error
            );

            if (failure == null &&
                !Contracts.ValidBase(result, session.player.id))
            {
                failure =
                    "O estado da base não corresponde ao contrato esperado.";
            }

            if (failure != null)
            {
                if (state == null)
                {
                    ShowLogin(failure);
                }
                else
                {
                    ui.Status(
                        failure + " Dados anteriores mantidos.",
                        true
                    );
                }

                yield break;
            }

            bool first = state == null;

            if (state != null && result.buildings.Any(b => Contracts.Produces(b.type) &&
                !state.buildings.Any(old => old.id == b.id && old.level == b.level && old.status == b.status)))
                production.Snapshot(null);
            state = result;
            lastRefreshSucceeded = true;

            if (first)
            {
                ui.Close();
            }

            ui.Hud(
                state,
                session.player.display_name
            );

            world.Render(
                state,
                selected,
                first
            );
            production.Render(state);
            if (!production.HasSnapshot) yield return RefreshProduction();

            ui.Status(
                production.HasSnapshot ? "Clique na construção pronta para coletar • EDIFÍCIOS abre os detalhes." : "Produção indisponível. Atualize para tentar novamente.", !production.HasSnapshot
            );

            yield return api.Send(
                "GET",
                PlayerPath + "/obstacles",
                null,
                json =>
                {
                    try
                    {
                        var obstacleList = JsonUtility.FromJson<ObstacleList>(
                            "{\"items\":" + json + "}"
                        );

                        if (obstacleList?.items == null) throw new InvalidOperationException("Obstáculos ausentes.");
                        world.Obstacles(obstacleList.items);
                        placementObstacles = obstacleList.items;
                    }
                    catch (Exception)
                    {
                        ui.Status(
                            "Base carregada; resposta de obstáculos inválida.",
                            true
                        );
                    }
                },
                error =>
                {
                    ui.Status(
                        "Base carregada; obstáculos indisponíveis. " + error,
                        true
                    );
                }
            );
        }

        void Update()
        {
            if (production != null) production.Hidden = ui.HasDialog || (placement != null && placement.IsActive);
            if (world != null) world.InputBlocked = busy || ui.HasDialog || (placement != null && placement.IsActive);
            if (state != null && !busy && !ui.HasDialog && !placement.IsActive && Time.unscaledTime >= nextRefresh) Run(Refresh());
        }

        void List()
        {
            if (state == null || busy || placement.IsActive) return;
            var panel = ui.Dialog("EDIFÍCIOS DA BASE");
            foreach (var b in state.buildings)
            {
                var id = b.id;
                FrontierView.Button(panel, Contracts.Name(b.type) + " • Nv. " + b.level + " • " + Contracts.State(b.status), () => OpenDetails(id));
            }
        }

        void Select(string id)
        {
            if (busy || state == null || placement.IsActive) return;
            if (production.Ready(id)) { ui.Close(); Act(id, "collect"); return; }
            OpenDetails(id);
        }

        void OpenDetails(string id)
        {
            if (busy || state == null || placement.IsActive) return;
            selected = id; world.Select(id);
            var building = state.buildings.FirstOrDefault(b => b.id == id);
            if (building != null) Run(Details(building));
        }

        IEnumerator Details(BuildingDto b)
        {
            // Recheck wallet/builders before showing actionable costs (no speculative debit).
            var loading = ui.Dialog(Contracts.Name(b.type)); ui.Busy(true);
            FrontierView.Text(loading, "Verificando recursos e construtores...");
            yield return Refresh();
            if (!ui.HasDialog) yield break;
            b = state.buildings.FirstOrDefault(item => item.id == b.id);
            if (b == null) { ui.Close(); yield break; }
            var panel = ui.Dialog(Contracts.Name(b.type)); ui.Busy(true);
            FrontierView.Text(panel, "NÍVEL " + b.level + "  /  " + Contracts.State(b.status), 20, 36);
            FrontierView.Button(panel, "MOVER NA GRADE", () => BeginPlacement(b.id));
            FrontierView.Text(panel, $"Durabilidade: {b.durability:N0} / {b.max_durability:N0}", 16, 30);
            if (b.status == "building")
            {
                var deadline = b.construction_finish_time;
                if (DateTimeOffset.TryParse(deadline, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
                    deadline = time.ToLocalTime().ToString("dd/MM HH:mm:ss");
                FrontierView.Text(panel, "Conclusão prevista: " + deadline + "\nAtualize para consultar o estado da obra no servidor.", 18, 90);
                FrontierView.Button(panel, "ATUALIZAR OBRA", () => { ui.Close(); Run(Refresh()); });
                yield break;
            }
            if (b.status == "ready_to_upgrade")
            { FrontierView.Button(panel, "CONCLUIR OBRA", () => Act(b.id, "confirm")); yield break; }
            if (Contracts.Produces(b.type) && b.status == "completed")
                FrontierView.Button(panel, "COLETAR RECURSOS", () => Act(b.id, "collect")).interactable = production.Ready(b.id);
            if (b.status != "completed" && b.status != "pending")
            { FrontierView.Text(panel, "Ações de reparo serão integradas em uma próxima etapa.", 18, 65); yield break; }
            UpgradeDto quote = null; string failure = null;
            yield return api.Get<UpgradeDto>(PlayerPath + "/buildings/" + b.id + "/next-upgrade", dto => quote = dto, error => failure = error);
            if (panel == null || !ui.HasDialog) yield break;
            if (failure != null) { FrontierView.Text(panel, failure, 17, 110); yield break; }
            if (quote == null || quote.building_id != b.id || quote.required_resources == null)
            { FrontierView.Text(panel, "Resposta de evolução inválida."); yield break; }
            FrontierView.Text(panel, $"PRÓXIMO NÍVEL  {quote.current_level} → {quote.next_level}", 20, 38);
            FrontierView.Text(panel, quote.required_resources.Summary(), 18, 60);
            FrontierView.Text(panel, "Tempo: " + quote.construction_time_formatted, 18, 32);
            var summary = new StringBuilder();
            if (quote.stat_effects != null)
                foreach (var field in typeof(EffectsDto).GetFields())
                {
                    var effect = field.GetValue(quote.stat_effects) as StatDto;
                    if (effect != null && (effect.current != 0 || effect.next != 0))
                        summary.AppendLine(EffectName(field.Name) + ": " + effect.current + " → " + effect.next);
                }
            if (summary.Length > 0) FrontierView.Text(panel, summary.ToString(), 16, Mathf.Max(38, summary.ToString().Count(c => c == '\n') * 25));
            if (quote.unlocked_features?.unlocks != null && quote.unlocked_features.unlocks.Length > 0)
                FrontierView.Text(panel, "Desbloqueia: " + string.Join(", ", quote.unlocked_features.unlocks), 16, 75);
            string blocker = ProductionRules.UpgradeBlocker(state, quote, lastRefreshSucceeded);
            FrontierView.Text(panel, blocker ?? "Recursos e construtor disponíveis. O servidor validará os demais pré-requisitos.", 15, 50);
            FrontierView.Button(panel, "INICIAR EVOLUÇÃO", () => Act(b.id, "upgrade")).interactable = blocker == null;
        }


        public void BeginPlacement(string id)
        {
            if (busy || placement.IsActive || state == null) return;
            if (!lastRefreshSucceeded || placementObstacles == null)
            { ui.Close(); ui.Status("Atualize a base e os obstáculos antes de mover.", true); return; }
            var building = state.buildings.FirstOrDefault(b => b.id == id);
            var model = world.BuildingModel(id);
            if (building == null || model == null) return;
            selected = id; world.Recenter(state);
            ui.ShowPlacement(placement.Confirm, placement.Cancel);
            placement.Begin(state, placementObstacles, building, world.ViewCamera, model);
        }
        IEnumerator MoveBuilding(string id, int x, int y)
        {
            string failure = null;
            ui.Status("Salvando posição no servidor...");
            yield return api.Send("PATCH", PlayerPath + "/buildings/" + Uri.EscapeDataString(id) + "/position",
                JsonUtility.ToJson(new MoveBuildingRequest { grid_x = x, grid_y = y }), json =>
                {
                    try
                    {
                        var result = JsonUtility.FromJson<MoveBuildingResponse>(json);
                        if (result == null || result.building_id != id || result.grid_x != x || result.grid_y != y)
                            failure = "Resposta de movimentação inesperada. Confira a posição atualizada.";
                    }
                    catch (Exception) { failure = "Resposta de movimentação inválida. Confira a base atualizada."; }
                }, error => failure = error);
            // No optimistic mutation or automatic retry: timeout may mean the server already saved.
            yield return Refresh();
            ui.Status(failure ?? (lastRefreshSucceeded ? "Posição salva e confirmada pelo servidor." : "Movimentação enviada, mas a base não pôde ser atualizada. Use ATUALIZAR."), failure != null || !lastRefreshSucceeded);
        }

        IEnumerator RefreshProduction()
        {
            ProductionStorageDto result = null;
            string failure = null;
            yield return api.Get<ProductionStorageDto>(PlayerPath + "/resources/production", dto => result = dto, error => failure = error);
            production.Snapshot(failure == null ? result : null);
            if (!production.HasSnapshot) ui.Status("Produção indisponível. Atualize para tentar novamente.", true);
        }

        static string EffectName(string name)
        {
            switch (name)
            {
                case "power": return "Poder";
                case "production_per_hour": return "Produção/h";
                case "max_storage_capacity": return "Armazenamento";
                case "hp": return "Vida";
                case "attack": return "Ataque";
                case "defense": return "Defesa";
                case "hospital_capacity": return "Capacidade do hospital";
                default: return name.Replace('_', ' ');
            }
        }

        void Act(string buildingId, string action) => Run(Mutate(buildingId, action));

        IEnumerator Mutate(string buildingId, string action)
        {
            string failure = null;
            CollectResourcesDto collection = null;
            var model = world.BuildingModel(buildingId);
            Vector3 feedbackPosition = model != null ? model.transform.position : Vector3.zero;
            if (model != null)
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    feedbackPosition.y = Mathf.Max(feedbackPosition.y, renderer.bounds.max.y);
            feedbackPosition += Vector3.up * .25f;
            yield return api.Send("POST", PlayerPath + "/buildings/" + buildingId + "/" + action, null, json =>
            {
                if (action != "collect") return;
                try
                {
                    collection = JsonUtility.FromJson<CollectResourcesDto>(json);
                    if (collection == null || collection.building_id != buildingId || collection.collected_resources == null)
                    { collection = null; failure = "Resposta de coleta inválida. Confira o saldo atualizado."; }
                }
                catch (Exception) { collection = null; failure = "Resposta de coleta inválida. Confira o saldo atualizado."; }
            }, error => failure = error);
            if (failure == null && collection != null && model != null)
                CollectionFeedback.Play(collection.collected_resources, feedbackPosition, transform);
            ui.Close();
            // Production GET resets fractional accumulation on the server: fetch at login and
            // after mutations only, never on every base poll. Reconcile failures too.
            production.Snapshot(null);
            // Reconcile even after a timeout: the server might have committed the action.
            yield return Refresh();
            ui.Status(failure ?? (lastRefreshSucceeded ? "Ação confirmada pelo servidor." : "Ação aceita, mas a atualização da base falhou. Use ATUALIZAR antes de repetir."), failure != null || !lastRefreshSucceeded);
        }

        IEnumerator Tutorial()
        {
            TutorialDto progress = null; string failure = null;
            yield return api.Get<TutorialDto>("/v1/tutorial/progress", dto => progress = dto, error => failure = error);
            var panel = ui.Dialog("ORIENTAÇÃO DO COMANDO");
            if (failure != null) FrontierView.Text(panel, failure, 18, 130);
            else
            {
                FrontierView.Text(panel, progress.completed ? "Tutorial concluído" : "Etapa " + progress.current_step, 23, 44);
                FrontierView.Text(panel, string.IsNullOrEmpty(progress.npc_message) ? "Nenhuma orientação adicional disponível nesta etapa." : progress.npc_message, 19, 240);
            }
        }

        void OnDisable() { if (placement != null) placement.End(); if (ui != null) ui.HidePlacement(); api?.Dispose(); StopAllCoroutines(); busy = false; }


        static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return "<empty>";
            }

            if (token.Length <= 12)
            {
                return "***";
            }

            return $"{token[..6]}...{token[^6..]}";
        }

    }
}
