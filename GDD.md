# GDD — Campeche

**Versão do documento:** 2.04  
**Estado do projeto:** build estável / candidata a lançamento  
**Engine:** Unity 6, C# e Universal Render Pipeline (URP)  
**Plataformas:** navegador (WebGL) e Windows 64 bits  
**Idiomas:** português do Brasil e inglês

## 1. Visão geral

**Campeche** é uma aventura de ação e progressão ambientada na Ilha do Campeche. O jogador assume o papel de um mergulhador que chega a um pequeno vilarejo e passa a explorar o oceano, enfrentar criaturas marinhas, recuperar riquezas e reinvestir seus ganhos na própria evolução, nos barcos e na reconstrução da ilha.

O jogo alterna entre dois estilos complementares:

- um cenário marítimo 3D estilizado, usado para navegação, exploração, interação com a ilha e gerenciamento da progressão;
- fases subaquáticas 2D em pixel art, focadas em combate, sobrevivência, coleta e desempenho.

A campanha termina quando a ilha alcança o nível máximo de desenvolvimento e o farol volta a brilhar, simbolizando que o vilarejo finalmente está seguro e preparado para um novo começo.

## 2. Contexto da Game Jam

O projeto foi desenvolvido em aproximadamente duas semanas para a **Uplifting Game Jam 9**, com o tema **Ocean**.

O conceito foi construído para transmitir esperança, reconstrução e crescimento. Embora existam perigos, derrotas e penalidades, a experiência mantém um tom leve, familiar e positivo: cada mergulho contribui para melhorar a vida da comunidade.

O projeto respeita as principais condições da jam: uso livre de engine, equipe pequena, entrega dentro do período estabelecido, conteúdo apropriado para público geral e uso de recursos próprios, gratuitos ou licenciados com os devidos créditos.

## 3. Pilares de design

### 3.1 Explorar

Navegar por um oceano estilizado, observar pontos de interesse e escolher quando retornar à ilha ou iniciar um novo mergulho.

### 3.2 Enfrentar desafios

Superar perigos diferentes nas seções 3D e 2D, aprendendo padrões de movimento e administrando vida, turbo, posicionamento e ataque.

### 3.3 Evoluir

Transformar o ouro encontrado em melhorias permanentes para o mergulhador, o arpão, a embarcação e a ilha.

### 3.4 Reconstruir

Fazer o vilarejo crescer visual e mecanicamente até que o farol seja restaurado e a campanha seja concluída.

## 4. Loop principal

1. O jogador parte da ilha com um barco.
2. Navega pelo cenário 3D, desviando de rochas, tubarões e tornados.
3. Interage com um ponto de mergulho.
4. Entra em uma fase subaquática 2D.
5. Derrota inimigos e libera baús conforme avança na missão.
6. Retorna com ouro e recompensas asseguradas.
7. Repara ou melhora equipamentos, barcos e estruturas da ilha.
8. Enfrenta uma etapa mais exigente e repete o ciclo até restaurar o farol.

## 5. Estrutura de cenas

O jogo utiliza duas cenas principais:

| Cena | Função |
| --- | --- |
| `OceanScene_3D` | Menu, navegação, ilha, porto, progressão, configurações e celebração final |
| `DiveScene` | Fases subaquáticas 2D, combate, inimigos, baús e recompensas |

O menu principal e o encerramento são apresentados dentro da cena do oceano. Não existem cenas independentes de menu ou de endgame na build atual.

As transições entre contextos utilizam fade suave. A passagem específica entre **Novo Jogo**, escolha de dificuldade e início da apresentação foi mantida direta, sem fade adicional, enquanto as demais transições continuam suavizadas.

## 6. Início de jogo e idiomas

Ao começar uma nova campanha, o jogador escolhe entre **Fácil**, **Normal** e **Difícil**. Um novo jogo limpa a progressão anterior, restaura todos os barcos para a vida máxima e inicia a campanha com 100 moedas de ouro.

Português do Brasil e inglês podem ser selecionados por bandeiras na interface. A escolha altera:

- menus e painéis;
- tutoriais e mensagens de interação;
- descrições de upgrades;
- carta de introdução;
- carta do primeiro barco destruído;
- mensagem e narração da conclusão.

As narrações localizadas são reproduzidas uma única vez, de acordo com o idioma ativo, sem interromper a possibilidade de avançar quando o texto termina de ser apresentado.

## 7. Navegação no oceano — 3D

### 7.1 Barco

O jogador controla direção e aceleração da embarcação. O turbo fornece um impulso temporário e possui recurso próprio no HUD. A sensação de navegação é reforçada por áudio de motor, feedback de colisão e espuma estilizada ao redor e atrás do casco.

Existem três barcos visualmente distintos. Novas embarcações são liberadas pela evolução do conjunto casco/turbo e podem ser selecionadas no porto. Ao serem liberadas, começam com a vida máxima.

### 7.2 Câmera

A câmera orbita um eixo central fixado no meio do barco, sempre mantendo a mesma distância. O jogador pode completar mais de uma volta ao redor da embarcação, com aceleração e desaceleração suaves e uma inclinação lateral discreta na direção do giro.

Ao soltar o controle, a câmera retorna lentamente ao ponto inicial atrás do barco pelo caminho angular mais curto. O enquadramento permanece levemente elevado, mas próximo da traseira da embarcação.

### 7.3 Perigos do oceano

- **Rochas:** obstáculos fixos que causam dano ao casco em colisões.
- **Tubarões:** patrulham o oceano, mergulham, detectam o barco, ajustam a direção e realizam investidas.
- **Tornados:** deslocam-se pelo mapa, atraem o barco para uma espiral, reduzem temporariamente o controle e podem arremessá-lo com dano.

Os barcos mais avançados suportam melhor esses perigos. No modo Fácil, determinados elementos hostis do oceano também são reduzidos para tornar a navegação mais acessível.

## 8. Mergulho — 2D

### 8.1 Movimento e combate

O mergulhador se desloca livremente em duas dimensões, pode usar dash e dispara o arpão mantendo o botão de ataque pressionado. O arpão evolui em velocidade, alcance, dano e capacidade de disparo ao longo da campanha.

Ao receber dano, o personagem pisca por aproximadamente 1,5 segundo, com uma máscara vermelha evidente e alpha reduzido, oferecendo retorno visual claro do impacto e do período de invulnerabilidade.

### 8.2 Objetivo da sessão

Cada mergulho possui uma quantidade total definida de inimigos. O progresso libera recompensas em quatro marcos:

- 25%: baú pequeno;
- 50%: baú pequeno;
- 75%: baú pequeno;
- 100%: baú final.

O baú final considera desempenho, incluindo tempo, dano sofrido e ritmo de eliminações. Ouro já assegurado é preservado conforme as regras da sessão, enquanto a conclusão eficiente oferece uma recompensa maior.

### 8.3 Inimigos

O elenco subaquático é formado por cinco espécies com comportamentos próprios:

| Inimigo | Comportamento principal |
| --- | --- |
| Água-viva | Movimento ágil e evasivo, dificultando tiros diretos |
| Peixe-espada | Aproximação agressiva e investidas rápidas |
| Polvo | Movimento mais astuto e irregular, com controle de espaço e repulsão |
| Sereia maga | Ataques à distância e preferência por manter espaço do jogador |
| Sereia guerreira | Combate próximo, perseguição, carga e empurrão |

A composição das ondas muda com o nível de progressão. Inimigos mais complexos passam a ter maior presença conforme o jogador compra melhorias.

### 8.4 Power-ups temporários

- **Arpão duplo:** dois projéteis com abertura angular por 10 segundos; deixa de aparecer quando a melhoria máxima do arpão já fornece esse benefício.
- **Escudo:** proteção temporária por 6 segundos.
- **Velocidade:** multiplica a movimentação por 1,5 durante 8 segundos.

## 9. Dificuldade escalável

A dificuldade escolhida não altera as regras dos outros modos. Ela controla principalmente o volume de inimigos e o intervalo de surgimento em cada um dos cinco níveis de progressão.

### 9.1 Fácil

| Nível | Iniciais | Total | Intervalo |
| --- | ---: | ---: | ---: |
| N1 | 5 | 15 | 3 s |
| N2 | 8 | 22 | 3 s |
| N3 | 10 | 24 | 3 s |
| N4 | 11 | 28 | 3 s |
| N5 | 12 | 35 | 3 s |

### 9.2 Normal

| Nível | Iniciais | Total | Intervalo |
| --- | ---: | ---: | ---: |
| N1 | 6 | 18 | 2 s |
| N2 | 12 | 30 | 2 s |
| N3 | 14 | 33 | 2 s |
| N4 | 15 | 38 | 2 s |
| N5 | 16 | 45 | 2 s |

### 9.3 Difícil

| Nível | Iniciais | Total | Intervalo |
| --- | ---: | ---: | ---: |
| N1 | 9 | 25 | 2,5 s |
| N2 | 15 | 35 | 2,5 s |
| N3 | 17 | 45 | 2,5 s |
| N4 | 19 | 51 | 2,5 s |
| N5 | 21 | 58 | 2,5 s |

O nível efetivo do mergulho é calculado a partir da quantidade de melhorias relevantes já compradas: ilha, embarcação, vida, velocidade e arpão.

## 10. Economia e progressão

O ouro é a moeda central. Ele é obtido principalmente nos mergulhos e gasto no porto. O HUD é sincronizado continuamente com o valor real armazenado para evitar divergências entre exibição e progresso.

### 10.1 Melhorias da ilha

| Evolução | Custo |
| --- | ---: |
| Nível 0 → 1 | 100 |
| Nível 1 → 2 | 200 |
| Nível 2 → 3 | 500 |
| Nível 3 → 4 | 900 |

Cada nível representa uma etapa da reconstrução. O nível 4 conclui a campanha e restaura o farol.

### 10.2 Embarcação

Casco e turbo evoluem juntos como uma única categoria visível de embarcação:

| Nível | Custo | Benefício principal |
| --- | ---: | --- |
| N1 | Inicial | Barco básico |
| N2 | 60 | +15% de casco e +20% de duração do turbo |
| N3 | 120 | +30% de casco e +40% de duração do turbo |

### 10.3 Mergulhador

| Categoria | N1 | N2 | N3 |
| --- | --- | --- | --- |
| Vida | 5 | 7 por 70 ouro | 9 por 140 ouro |
| Velocidade | base | +15% por 60 ouro | +30% por 120 ouro |

### 10.4 Arpão

| Evolução | Custo |
| --- | ---: |
| N1 → N2 | 80 |
| N2 → N3 | 160 |
| N3 → N4 | 280 |

No nível máximo, o arpão alcança a configuração final de combate, incluindo 15 de dano, cadência de 1 segundo, velocidade de 12 unidades por segundo, dois disparos, alcance de 20 unidades por arpão e perfuração de até dois alvos.

## 11. Dano, destruição e reparo

Cada barco mantém seu próprio valor de casco. A primeira destruição de uma campanha é reparada gratuitamente pelo vilarejo e apresenta uma mensagem narrativa explicando a ajuda.

Nas destruições seguintes:

- o modo Fácil utiliza custo-base de 125 moedas;
- os demais modos utilizam o custo configurado para recuperação;
- se o jogador não possuir o total necessário, a cobrança é limitada à metade do ouro disponível.

Melhorias permanentes não são perdidas ao morrer. O reparo também pode ser feito no porto, com custo proporcional ao dano; um reparo completo custa até 150 moedas.

## 12. Controles

### 12.1 Oceano

| Ação | Controle |
| --- | --- |
| Mover e virar | WASD ou setas |
| Turbo | Shift |
| Interagir | F ou clique |
| Girar câmera | Botão direito do mouse |
| Pausar | P |

### 12.2 Mergulho

| Ação | Controle |
| --- | --- |
| Nadar | WASD ou setas |
| Dash | Shift |
| Atacar | Segurar botão esquerdo do mouse |
| Interagir | F |
| Pausar | P |

Objetos interativos apresentam, na primeira aproximação, a instrução localizada equivalente a **“Pressione F ou Clique”** quando ambas as entradas são aceitas.

## 13. Interface e acessibilidade

- HUD de casco, turbo e ouro no oceano.
- HUD de vida, progresso e recompensas no mergulho.
- Painéis responsivos para diferentes proporções de tela e modo tela cheia.
- Textos em fonte padronizada e configurada para preservar legibilidade.
- Tooltips das bandeiras posicionados em relação ao próprio Canvas.
- Configurações de volume geral, música, efeitos e sensibilidade de direção.
- Pausa disponível nos dois modos de jogo.
- Tutoriais apresentados apenas quando necessários, com painéis dimensionados ao conteúdo.

## 14. Direção de arte e efeitos

O oceano utiliza visual 3D estilizado, colorido e low-poly. O mergulho emprega pixel art 2D, criando uma transição intencional de perspectiva e linguagem visual sem abandonar o mesmo universo de fantasia.

Elementos importantes da apresentação incluem:

- água brilhante e espuma estilizada nas margens da ilha;
- rastro de espuma no barco quando está em movimento;
- horizonte suavizado para reduzir a divisão rígida entre mar e céu;
- iluminação do farol amarela, vibrante e com sensação de calor;
- skybox configurável pelo Inspector, com iluminação ambiental equilibrada;
- feedbacks de dano, colisão, coleta, desbloqueio e interação.

As pastas locais de artes e artes importadas da Unity permanecem fora do repositório público por conterem materiais sujeitos a licenças de terceiros. A distribuição do jogo mantém os créditos e as permissões exigidas por cada pacote utilizado.

## 15. Áudio

O áudio reforça navegação, combate, recompensas e narrativa. O projeto inclui:

- música e ambientação marítima;
- motores e turbo específicos das embarcações;
- impactos, ataques, coleta de moedas e feedbacks de interface;
- narrações em português e inglês para a introdução, primeiro reparo e conclusão;
- controles separados de volume geral, música e efeitos.

Os áudios narrativos não entram em loop e começam junto da redação ou exibição da mensagem correspondente.

## 16. Salvamento

O progresso é persistido localmente por um sistema próprio baseado em `PlayerPrefs` e dados serializados em JSON. O formato atual do save é versionado para permitir validação e futuras migrações.

Entre os dados armazenados estão:

- campanha iniciada e introdução concluída;
- idioma, dificuldade e configurações;
- ouro total;
- níveis de melhoria;
- barco selecionado;
- vida atual e máxima de cada barco;
- quantidade de destruições;
- estado de conclusão da ilha.

Confirmar **Novo Jogo** reinicializa os dados da campanha, inclusive a vida de todos os barcos, sem reaproveitar danos do save anterior.

## 17. Encerramento

Ao comprar o quarto nível da ilha, o farol é restaurado e começa a celebração final. A câmera aérea inicia uma volta panorâmica de 360 graus, lenta e suave, para apresentar todas as conquistas do jogador.

O painel de agradecimento e a narração localizada surgem no começo da sequência. A mensagem permanece por 10 segundos e desaparece em um fade de 1,5 segundo. A câmera continua a apresentação da ilha independentemente do painel, preservando o momento de contemplação.

## 18. Ferramentas internas de teste

Duas sequências podem ser digitadas em até três segundos durante a navegação livre da `OceanScene_3D`, desde que nenhum painel esteja aberto:

- `hesoyam`: reproduz o som de ativação e recupera o casco suavemente até 100%;
- `twenty`: adiciona 20.000 moedas de ouro imediatamente.

Esses comandos existem como ferramentas internas e easter eggs. Eles não funcionam em menus, no mergulho ou enquanto a interface bloqueia a navegação.

## 19. Decisões de escopo consolidadas

O planejamento inicial continha ideias que foram substituídas durante a produção. A versão 2.04 considera como definitivas as seguintes decisões:

- a navegação principal é 3D low-poly, e não 2D pixel art;
- somente o mergulho é apresentado como fase 2D;
- não há mapa aberto pela tecla M;
- não há geração procedural por chunks;
- não há inventário, crafting, sistema de missões ou árvore de habilidades separada;
- a progressão acontece diretamente por ouro e upgrades no porto;
- menus e endgame fazem parte da cena 3D, sem cenas exclusivas;
- o escopo final possui cinco inimigos subaquáticos, três barcos, três dificuldades e quatro níveis de reconstrução da ilha.

## 20. Critérios da versão estável 2.04

- campanha completa do novo jogo ao acendimento do farol;
- saves reiniciados corretamente e persistência validada;
- português e inglês disponíveis em toda a experiência principal;
- três dificuldades funcionando com tabelas independentes;
- progressão, reparos, barcos, inimigos, recompensas e HUD sincronizados;
- transição entre navegação 3D e combate 2D funcional;
- interfaces ajustadas para WebGL, tela cheia e resoluções menores;
- build disponível para navegador e preparada para executável Windows;
- artes licenciadas protegidas do histórico público do Git.

## 21. Links

- **Jogar:** [https://sedaking.itch.io/campeche](https://sedaking.itch.io/campeche)
- **X / Twitter:** [@SKStudios_Games](https://x.com/SKStudios_Games)
- **GitHub:** [rafaelcpalhano-blip](https://github.com/rafaelcpalhano-blip)

