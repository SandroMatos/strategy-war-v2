# Arquitetura atual — frontend Unity e FastAPI

Atualizado em 17/09/2026. Projeto: `C:\projetos\LastWarsClone\My project`.

## Resultado

Foi criada uma primeira versão funcional do cliente **Iron Frontier**, mantendo **Unity 2021.3.45f2**. A cena de entrada é `Assets/LastWars/Scenes/Frontier.unity`. A cena original SampleScene continua intacta e permanece na lista de build, após a nova entrada.

O cliente possui entrada de comandante, criação de sessão anônima, retomada da sessão local, base isométrica, recursos e construtores, obstáculos, seleção de edifícios, lista de edifícios, custos/efeitos do próximo nível, coleta, início e confirmação de evolução e consulta da orientação do tutorial.

Tudo é montado por código: câmera, iluminação, geometria, Canvas, textos TMP, campos, botões, EventSystem e referências. Não é necessário arrastar componentes no Inspector. Esta é uma base funcional com geometria provisória; não representa ainda a qualidade artística das referências.

## Como abrir

1. Abra o projeto com **Unity 2021.3.45f2**.
2. Use **LastWars → Abrir frontend**. A ferramenta preserva alterações não salvas por meio do diálogo padrão do Editor.
3. Pressione **Play**.
4. Informe um nome de comandante de 3–32 caracteres para criar uma conta no backend. A conta e a base permanecem no servidor; a sessão é salva neste dispositivo.

O frontend usa `http://127.0.0.1:8000`, configurado em `Assets/LastWars/Resources/FrontierConfiguration.asset`. Essa URL foi confirmada como **Iron Frontier API** em 17/09. O grupo Docker indicado pelo usuário é `que`; não foram reiniciados containers ou alteradas suas portas.

Arraste a base para mover a câmera e use a roda para aproximar. Também há implementação de arraste/pinça para toque, ainda sem teste em dispositivo físico. Clique num edifício ou use **EDIFÍCIOS** para abrir detalhes. Painéis extensos possuem rolagem. **ATUALIZAR** consulta novamente o servidor.

## Referências analisadas

Foram examinadas 50 imagens em `C:\Users\USER\OneDrive\Pictures\lastwars`, incluindo subpastas de evolução da base, evolução de construções e diálogos.

| Grupo visual | Observação | Aplicação nesta etapa |
|---|---|---|
| Base | Terreno desértico, perspectiva isométrica, edifícios em grade, recursos no topo | Câmera ortográfica, terreno, posições e dimensões do backend, HUD |
| Evolução | Janela por edifício, nível atual/próximo, custos, efeitos e tempo | Consulta next-upgrade, recursos, atributos e botão de evolução |
| Heróis | Catálogo por tipo, raridade, atributos, habilidades e tier | Mapeado para evolução futura; telas ainda não implementadas |
| Missões | Objetivos de capítulo e navegação por ação | Consulta inicial de orientação do tutorial; lista completa de missões futura |
| Conversação | Retrato do personagem e caixa de diálogo sobre a base | Painel textual do tutorial; retratos/animações futuros |
| Construção | Obras, restauração, coleta e expansão | Evolução/coleta integradas; posicionamento/expansão e reparo futuros |

As capturas foram usadas como referência de composição e fluxo. Não foram copiadas como texturas nem usadas para simular estado de jogo.

## Arquitetura implementada

| Arquivo em Assets/LastWars | Responsabilidade |
|---|---|
| Runtime/FrontierApp.cs | Orquestra sessão, navegação, consultas, ações e reconciliação com o servidor |
| Runtime/ApiClient.cs | UnityWebRequest via coroutines, Bearer, timeout de 15 s, erros e cancelamento |
| Runtime/ApiContracts.cs | DTOs C# de transporte, validação de identidade e nomes de apresentação |
| Runtime/ClientConfiguration.cs | ScriptableObject de URL/intervalo de atualização |
| Runtime/FrontierView.cs | uGUI + TMP, formulários, HUD, painéis roláveis, escala e safe area |
| Runtime/BaseWorld.cs | Câmera isométrica, geometria temporária, seleção e obstáculos |
| Runtime/BuildingMarker.cs | Identificação do edifício atingido pelo raycast |
| Editor/FrontierEditor.cs | Abertura segura da cena e verificações de serialização no Unity |
| Editor/FrontierBatchValidation.cs | Teste integrado explícito, capturas e restauração da sessão local |
| Resources/FrontierConfiguration.asset | Configuração de desenvolvimento |
| Scenes/Frontier.unity | Entrada do frontend; FrontierApp já associado |

Não foram adicionados packages, asmdefs ou dependências externas. Foram reutilizados URP 12.1.15, uGUI 1.0.0, TMP 3.0.6 e Input Manager legado. O namespace novo é `LastWars.Client`; não existe singleton global. Os scripts/cena originais não foram substituídos. Os .meta criados são dos novos assets; GUIDs antigos foram preservados.

Os mapas JSON de recursos e efeitos têm chaves conhecidas no domínio do backend e são representados por classes com campos específicos, permitindo usar JsonUtility sem introduzir um serializador. Campos nullable não utilizados não são forçados para inteiros; datas usadas na UI permanecem strings. Alterações futuras nos enums/esquemas devem atualizar os DTOs e suas verificações.

## Integração

Backend de referência: `C:\Users\USER\Documents\Codex\2026-09-08\que\backend`. O código FastAPI e o OpenAPI real orientaram os contratos.

| Método | Rota | Uso |
|---|---|---|
| POST | /v1/auth/anonymous | Cria sessão e recebe access_token, token_type e player |
| GET | /players/{player_id}/base | Recursos, construtores, edifícios, estados e grade |
| GET | /players/{player_id}/obstacles | Obstáculos para renderização |
| GET | /players/{player_id}/buildings/{building_id}/next-upgrade | Custos, duração, efeitos e desbloqueios |
| POST | /players/{player_id}/buildings/{building_id}/collect | Coleta autoritativa |
| POST | /players/{player_id}/buildings/{building_id}/upgrade | Inicia obra |
| POST | /players/{player_id}/buildings/{building_id}/confirm | Aplica conclusão após ready_to_upgrade |
| GET | /v1/tutorial/progress | Orientação do tutorial |

A atualização automática ocorre a cada 10 segundos, sem concorrência com ações/painéis. Requisições não bloqueiam a main thread. Ao desativar o controlador, requisições são abortadas e descartadas. Operações mutáveis não têm retry automático: após ação ou timeout, o cliente tenta reler a base para conciliar o resultado.

Em 401, a sessão é preservada e nenhuma conta nova é criada silenciosamente. O token é salvo em PlayerPrefs, separado por URL da API, somente como persistência inicial de desenvolvimento. Não há fluxo de renovação/recuperação porque o backend ainda não oferece esse contrato. A solução de credenciais deve ser revista antes de publicar o jogo.

## Validação e evidências

- Compilação C# com Roslyn da instalação Unity 2021.3.45f2: assemblies de runtime e Editor aprovados durante a implementação.
- Importação no Unity real: `Library/FrontierValidation.txt` registrou PASS para DTOs, identidade, precisão dos recursos, datas null, evolução, listas e configuração.
- Teste HTTP real: **13 verificações aprovadas**, incluindo ausência de Bearer → 401, sessão, base/grade, obstáculos, tutorial, custos, coleta, evolução e validação do prazo de conclusão. Usou jogador QA dedicado.
- Teste em **Play Mode** no Editor aberto: **PASS** para criação de sessão, carregamento, apresentação de custos e início de evolução com reconciliação de recursos/construtores. Capturas de login, detalhes e base foram inspecionadas.
- O teste integrado usa uma conta QA nova e restaura a sessão local anterior ao terminar. Contas QA permanecem no banco; nenhum jogador existente foi usado para gastar recursos.
- A tentativa inicial em uma segunda instância batch foi bloqueada pela comunicação com o serviço de licença. A validação em Play Mode foi realizada posteriormente no Editor aberto, superando esse bloqueio.

Ferramentas reproduzíveis: **LastWars → Validar contratos do frontend** e **LastWars → Validar fluxo em Play Mode (conta QA)**. O segundo teste faz alterações somente na conta QA que cria e salva evidências em `Library/FrontierProbe`. Não deve ser executado contra produção. Não roda automaticamente ao abrir o projeto.

Não foram validados player build, Android/iOS físicos, WebGL, recuperação de conta ou o conjunto completo de features do backend. Os ajustes finais de cancelamento e mensagem de reconciliação devem ser considerados separadamente do primeiro teste Play Mode nas evidências cronológicas.

## Próximas etapas de desenvolvimento

1. Substituir geometria provisória por prefabs e arte próprios, com estados visuais de nível/obra.
2. Implementar missões de capítulo e navegação guiada usando os contratos existentes.
3. Implementar catálogo/telas de heróis, habilidades e progressão.
4. Integrar reparo, treinamento, movimentação/construção e mapa global por etapas.
5. Definir autenticação/recuperação e ambiente de publicação antes de distribuição.

Nenhuma decisão do usuário bloqueia o uso desta primeira etapa. O projeto permanece na versão Unity escolhida.


## Atualização de 18/09/2026 — posicionamento em grade

Implementado Grid-Based Building Placement: GridPlacementController controla prévia, grade e confirmação; GridPlacementRules valida footprint/limites/colisões sem modificar o snapshot. FrontierApp mantém obstáculos da última consulta, suspende atualização durante a prévia e envia PATCH /players/{player_id}/buildings/{building_id}/position. FrontierView oferece MOVER NA GRADE, feedback e confirmar/cancelar. Não requer configuração manual ou pacotes adicionais.

12 testes puros C#, 8 testes HTTP e o fluxo em Play Mode passaram. A funcionalidade move edifícios existentes; criação arbitrária e rotação dependem de contratos ainda ausentes no backend. Ver docs/GRID_BUILDING_PLACEMENT.md para uso, arquivos e limitações.
