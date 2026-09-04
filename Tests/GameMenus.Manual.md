# Menus — verificação no Unity e na build Web

Os menus são criados em runtime pelo GameMenus. Não duplicar a OceanScene_3D
nem adicionar outra cópia do componente à cena. A primeira cena da build deve
ser OceanScene_3D; DiveScene também deve estar na Scene List.

1. Com progresso existente, abrir Play em OceanScene_3D. Confirmar Continue,
   Novo jogo e Ajustes; aldeia no nível salvo e HUD de jogo oculto. WASD,
   Shift, F e cliques na boia/porto não devem agir. A câmera deve ficar parada.
2. Cancelar Novo jogo: confirmar que ouro e upgrades não mudaram. Confirmar
   Novo jogo somente com um save de teste: aldeia N1, ouro zero, upgrades N1.
   Preferências de volume/curva devem permanecer.
3. Sem progresso salvo, confirmar que o menu oferece Iniciar. Iniciar, parar
   e executar novamente: agora deve oferecer Continuar, mesmo com zero ouro.
   Não apagar PlayerPrefs de outros sistemas para executar este teste.
4. Ajustar volume e curva, sair e reabrir Play/build. Valores persistem.
   Curva permanece entre 85% e 115%; velocidade máxima não deve mudar.
5. Continuar: HUD volta, barco responde. Ir ao mergulho e retornar normalmente:
   não deve reabrir o menu inicial, e o barco retorna à boia como antes.
6. Apertar P com barco em movimento/turbo. Esperar e usar WASD/F/Shift/cliques:
   barco, câmera, física e carga do turbo ficam congelados. P/Continuar retoma
   o movimento sem zerar a inércia. Ajustes também permitem sair com P.
7. Na DiveScene, pausar durante dash, imunidade e emissão de bolhas. Esperar
   mais de 5 segundos: cooldowns, inimigos, efeitos e imunidade não avançam.
   Ao retomar, continuam do mesmo instante. Cliques não disparam arpões nem
   abrem baús atrás do menu.
8. Coletar moedas/baús pequenos e Voltar à ilha. Confirmar o ouro exatamente
   uma vez, sem prêmio do baú final fechado. Barco volta à posição inicial
   da cena junto à ilha (não à boia). Repetir com Voltar ao menu e Continuar.
9. Voltar ao menu desde o oceano: progresso permanece e aldeia correta aparece.
   Confirmar que não permanece Time.timeScale=0 após Continuar.
10. Painel de upgrades/resultados: P não deve criar um segundo pause por cima
    deles. Fechar/continuar esses painéis deve manter o comportamento anterior.
11. Repetir testes principais numa build Web, em 16:9 e janela menor. Confirmar
    todos os botões/sliders acessíveis e sem sobreposição com o HUD.
12. A abertura deve mostrar a câmera de menu antes do primeiro frame visível,
    sem controles/HUD. Iniciar/Continuar/Novo jogo confirmado fazem um arco de
    aproximadamente 4 segundos, com o barco ancorado e o mundo animado. Só depois aparecem
    HUD e controles. P/cliques repetidos durante o arco não devem interrompê-lo.
    Retomar um pause e retornar de um mergulho não devem repetir esse arco.
13. Na Main Camera > Camera Follow 3D, ajustar Main Menu View ou usar o menu
    de contexto Capture Scene View as Menu View, fora do Play, e salvar a cena.
    Confirmar que esse enquadramento não altera o Offset do acompanhamento.
14. Abrir o jogo repetidamente (com/sem save), inclusive com as opções de entrada
    rápida no Play do Editor. O menu deve abrir em toda nova execução do oceano.
    Durante menu e transição, ondas/balanço visual continuam, mas WASD, Shift,
    F e cliques não controlam o barco. O Rigidbody é restaurado ao iniciar.
    Pause P continua congelando todo o jogo, ao contrário do menu inicial.

Escopo do Continue: retoma ouro e upgrades persistidos; não restaura um mergulho
em andamento nem a posição exata do barco entre execuções do aplicativo.
O volume geral usa AudioListener.volume e já valerá para futuros AudioSources.

## Carta, áudio e rochas

## Naufrágio e conserto

- Zerar o casco por colisões: barco desce por 3,5 s com balanço gradual; não
  pode navegar, interagir ou abrir pause durante o afundamento.
- Depois retorna à posição inicial da cena junto ao píer, inclusive se a cena
  havia sido carregada retornando de uma boia. Casco/turbo cheios; ouro/upgrades
  mantidos. Conferir câmera, colisores e balanço normal após o resgate.
- Carta de resgate sorteia uma das oito mensagens; abertura + Writing duas vezes
  precisam ser audíveis apesar do pause da carta. Após o retorno ao píer, a câmera
  usa o ângulo do menu, 12% mais próxima do barco (Rescue View Approach).
  Clique/WASD/setas inicia a transição suave desse enquadramento para trás do
  barco antes de liberar controles. Repetir dois naufrágios.
- Conserto: casco cheio desabilita botão; 30% de dano custa 45, 50% custa 75,
  90% custa 135 (Full Repair Cost = 150). Saldo exato permite reparar, insuficiente
  bloqueia. Cliques repetidos não cobram novamente após encher o casco.
- Consertar não libera os controles enquanto o painel de upgrades está aberto.
  Custo usa proporção do casco máximo atual, também após upgrades.
- Alterar Full Repair Cost no PortUpgradePanel fora do Play e salvar a cena.
  Valores zero permitem reparo gratuito, sem tentar uma compra de valor zero.

- Iniciar sem save e confirmar Novo jogo devem exibir a carta ANTES da câmera.
  Continuar nunca deve exibi-la. O clique que abriu a carta não a fecha.
- Ouvir Textos e painels uma vez, seguido por Writing duas vezes, sem sobrepor.
  Dispensar por clique/WASD/setas cancela os sons da carta e inicia o arco.
- Ambiente deve tocar em loop no oceano, inclusive no menu e na carta, parar
  no mergulho e voltar no oceano. Não deve acumular cópias de AudioSource.
- Volume geral, fundo e efeitos persistem entre execuções; fundo não afeta
  Writing e efeitos não afeta ambiente. Efeitos futuros devem usar GameAudio.PlayEffect.
- Colidir com rochas: perder 30% do casco MÁXIMO (100 -> 70 -> 40 -> 10 -> 0),
  respeitando 0,75 s de intervalo por rocha, inclusive com múltiplos colisores.
  Conferir também as rochas de aldeias inicialmente inativas após upgrade.
- As rochas recebem configuração em runtime, identificadas por Rock/Rocks e
  variantes de instância. Se não houver colisores, são criados MeshColliders.
  Verificar fisicamente todas as pedras na cena; nomes diferentes exigem configuração.
- Assets/Resources/OceanAudioSettings contém os três clips e ganho de ambiente.
  Os arquivos de som em Assets/Artes estão ignorados pelo Git intencionalmente;
  distribuir/importar esses assets separadamente ao preparar outra máquina.
