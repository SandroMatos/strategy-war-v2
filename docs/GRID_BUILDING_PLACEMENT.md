# Grid-Based Building Placement — Unity 2021.3.45f2

## Uso

1. Abra **LastWars → Abrir frontend** e entre em Play.
2. Entre na sua base, selecione um edifício (ou use **EDIFÍCIOS**) e pressione **MOVER NA GRADE**.
3. Clique/toque ou arraste sobre o terreno. A prévia acompanha a grade e considera toda a largura e altura do edifício.
4. Verde indica posição livre; vermelho indica colisão, setor bloqueado ou posição original. O painel explica o motivo.
5. Use **CONFIRMAR POSIÇÃO** para salvar no FastAPI. **CANCELAR**, Escape ou botão direito encerram a prévia sem enviar alterações.

Durante a prévia, a câmera fica centralizada e o deslocamento da câmera e as consultas automáticas são suspensos. Mover o cursor até o botão de confirmação não altera a célula escolhida: a prévia só acompanha clique/arraste. Os edifícios originais permanecem visíveis até a resposta autoritativa do servidor.

## Contrato e regras

`PATCH /players/{player_id}/buildings/{building_id}/position`

```json
{"grid_x": 6, "grid_y": 8}
```

Bearer obrigatório. Response: `building_id`, `grid_x`, `grid_y`, `width`, `height`. Usa o endpoint existente sem alterar o backend.

Os limites vêm de `GET /players/{player_id}/base`, incluindo `grid_bounds`. O limite superior é exclusivo para células; o retângulo do edifício pode terminar exatamente nele. Colisões são verificadas contra outros edifícios e obstáculos não removidos. A própria construção é excluída da checagem. Bordas encostadas são válidas.

A prévia não altera o snapshot da base. Após confirmar, o cliente valida a resposta e recarrega o estado, inclusive após timeout/rejeição. Não há retry automático de PATCH. Se obstáculos não foram carregados ou a última consulta da base falhou, a ferramenta pede atualização em vez de assumir terreno livre.

Esta etapa posiciona construções existentes no estado do servidor, inclusive as que estão pendentes. Não cria novas instâncias arbitrárias nem implementa rotação: o backend atual não oferece esses contratos.

## Arquivos

- `Runtime/GridPlacementRules.cs`: validação pura e DTOs do PATCH.
- `Runtime/GridPlacementController.cs`: grade, clone visual sem colisores, encaixe, mouse/toque e ciclo da prévia.
- `Runtime/FrontierView.cs`: painel, feedback, confirmar/cancelar.
- `Runtime/FrontierApp.cs`: snapshot de obstáculos, entrada da ferramenta, exclusão de operações concorrentes e integração HTTP.
- `Runtime/BaseWorld.cs`: acesso à câmera/modelo e reinicialização do arraste quando bloqueado.
- `Editor/GridPlacementRulesTests.cs`: testes executáveis das regras.
- `Editor/FrontierEditor.cs` e `Editor/FrontierBatchValidation.cs`: testes no Unity e fluxo integrado de posicionamento.

Não requer associação manual no Inspector nem novos packages. Mantém as alterações preexistentes do projeto.

## Validação

- Compilação dos scripts com Roslyn do Unity 2021: aprovada.
- 12 testes C# das regras: aprovados (limites, footprint retangular, colisões parciais, bordas adjacentes, obstáculo removido, dados ausentes, overflow e imutabilidade).
- 8 testes HTTP no backend real: aprovados (sessão QA, posição válida, colisões de edifício/obstáculo, limites, input negativo, persistência após rejeição e carteira sem débito).
- Play Mode real: aprovado para prévia, colisão, cancelamento, PATCH e reconciliação.
- O teste integrado do menu **LastWars → Validar fluxo em Play Mode (conta QA)** inclui prévia inválida/válida, cancelamento, PATCH e reconciliação. Ele cria uma conta QA e restaura a sessão local anterior. Não usar em produção.
- Toque implementado, mas não testado em dispositivo físico. Player build não realizado nesta tarefa.
