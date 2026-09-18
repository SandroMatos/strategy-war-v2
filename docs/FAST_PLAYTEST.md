# Produção rápida para testes

Servidor: Iron Frontier API na porta 8000. Unity: 2021.3.45f2.

Os valores abaixo foram sorteados com uma semente fixa por tipo. Não mudam a cada atualização da tela. O servidor calcula estoque, tamanho da coleta e crédito; o Unity usa `collection_target` e `production_per_hour` da API para desenhar a barra.

| Construção | Unidades por coleta | Produção/h no nível 1 | Tempo aproximado, estoque vazio |
|---|---:|---:|---:|
| Fazenda | 105 | 3482 | 109 s |
| Mina de ferro | 100 | 3316 | 109 s |
| Refinaria de ouro | 99 | 3543 | 101 s |
| Bomba de petróleo | 100 | 3734 | 97 s |

A taxa cresce proporcionalmente ao nível. Cada clique transfere uma coleta; estoque excedente permanece na construção. A carteira continua respeitando seu limite de armazenamento. Consultas frequentes preservam o tempo fracionário de produção, evitando perder progresso entre atualizações.

O balanceamento temporário está centralizado em `backend/app/domain/resource_playtest.py`. Para sortear outro conjunto, altere a semente. A consulta de próximo nível mostra as mesmas taxas usadas na produção real.

## Nova conta

O cadastro agora cria sete construções: sede, quartel, campo de treinamento, uma mina de ferro, uma fazenda, uma refinaria de ouro e uma bomba de petróleo. Todos os geradores começam no nível 1, sem estoque pré-carregado, para testar o ciclo desde o início.

A conta **lelalalallaa** foi removida por solicitação do usuário, com seus registros associados. Os demais jogadores foram preservados. A sessão local correspondente também foi limpa. O próximo Play Mode abre o cadastro; nenhuma nova conta pessoal foi criada automaticamente.

## Validação

- 9 testes Python: taxas/limites, lotes, estoque excedente, frações de tempo, limite de armazenamento e base inicial sem colisões/duplicações.
- 17 testes C#: compatibilidade com alvo padrão, alvos menores/maiores que 100, contagem, recursos e disponibilidade de evolução.
- Runtime e Editor compilados com as referências do Unity 2021.3.45f2.
- Teste na API com conta QA: sete construções, um produtor por recurso, coleta exata de cada lote, crédito correspondente e taxas coerentes na evolução. O tempo foi avançado somente nos registros dessa conta QA para não esperar o ciclo inteiro.

Os números são de teste; não representam o balanceamento final do jogo.
