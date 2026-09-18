# Interface de produção e recursos — Unity 2021.3.45f2

Projeto: `C:\projetos\LastWarsClone\My project`.

## Comportamento

- Construções produtoras mostram uma barra e segundos restantes até acumular o lote coletável informado pelo servidor. Ao atingir esse ponto, exibem o ícone do recurso e “Coletar”. Edifícios sem produção de recursos não exibem uma contagem fictícia.
- Clicar no edifício pronto envia a coleta diretamente, sem abrir detalhes. A carteira só é atualizada com a resposta do servidor e a consulta posterior da base. Cliques repetidos são bloqueados durante a operação.
- Os detalhes e a movimentação continuam acessíveis por **EDIFÍCIOS**, mesmo enquanto há produção pronta.
- Ao abrir detalhes, o cliente atualiza o saldo e os construtores antes de consultar o custo do próximo nível. Evolução fica desabilitada se faltarem recursos, construtor ou uma consulta válida. O motivo aparece acima do botão.
- O painel do comandante mostra ícones vetoriais próprios para comida, ferro, ouro e petróleo, com nomes e saldos individuais. Os mesmos ícones são usados sobre as construções.
- Indicadores acompanham a câmera e ficam ocultos durante diálogos e posicionamento na grade. Eles não interceptam os cliques no mundo.

## Integração existente

Somente código Unity foi alterado. API em `http://127.0.0.1:8000`:

- `GET /players/{id}/base`: carteira, edifícios e construtores.
- `GET /players/{id}/resources/production`: produção/h, estoque e capacidade locais.
- `POST /players/{id}/buildings/{building_id}/collect`: coleta autoritativa.
- `GET /players/{id}/buildings/{building_id}/next-upgrade`: custos e próximo nível.

O Unity usa collection_target e production_per_hour do servidor. O perfil rápido de teste produz aproximadamente uma unidade por segundo no nível 1 e define lotes sorteados e estáveis por tipo. Consulte FAST_PLAYTEST.md para os valores atuais. O servidor transfere somente um lote por clique, preservando o estoque excedente. O contador usa tempo monotônico local a partir do recebimento da consulta.

A barra agora preenche o painel inteiro e o tempo fica centralizado dentro dela. A coleta confirmada emite um prefab ParticleSystem do recurso correspondente, com um burst de 18 ícones, duração visual de até 2,5 segundos e autodestruição. Não há partículas em falhas de coleta ou quando o servidor retorna zero.

O servidor preserva o tempo fracionário de produção entre consultas, sem perder progresso por polling frequente. O cliente atualiza a produção ao conectar, após ações e quando muda o estado ou nível de um produtor. A prévia usa os dados da API; o servidor decide a quantidade efetivamente transferida.

O contrato de próximo nível não fornece uma avaliação completa dos pré-requisitos de evolução. O cliente verifica recursos e construtores; demais restrições continuam validadas pelo servidor.

## Arquivos

Em `Assets/LastWars/Runtime`: novos `ProductionRules.cs`, `BuildingProductionView.cs` e `ResourceIcon.cs`; alterações em `FrontierApp.cs` e `FrontierView.cs`.

Em `Assets/LastWars/Editor`: novos testes `ProductionRulesTests.cs`; validação integrada em `FrontierEditor.cs` e `FrontierBatchValidation.cs`.

## Verificação

- Runtime e Editor compilados usando o compilador e referências do Unity 2021.3.45f2.
- 17 testes de duração, arredondamento, estoque já disponível, produção nula, saldo exato, insuficiência, construtores e dados desatualizados.
- Play Mode aprovado: coleta sem diálogo, crédito na carteira, botões habilitado/desabilitado, renderização dos ícones e regressão do posicionamento na grade.
- Teste em Play Mode disponível em **LastWars → Validar fluxo em Play Mode (conta QA)**. Usa uma conta de teste e restaura a sessão local anterior ao terminar.

Build mobile e toque em dispositivo físico não fazem parte desta validação.


