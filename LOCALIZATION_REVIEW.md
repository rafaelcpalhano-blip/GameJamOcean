# Relatório de localização PT-BR / EN

## Implementação

- Sistema central expandido: `LocalizationManager`, com tabelas PT-BR/EN, chaves estáveis, parâmetros dinâmicos e evento `LanguageChanged`.
- Idioma padrão na primeira execução: English.
- Persistência: `PlayerPrefs`, chave `GameJamOcean.Settings.Language` (`en` ou `pt-BR`).
- Seletor: bandeiras Brasil/EUA no menu Ajustes, recortadas por UV do atlas `Assets/Artes/UI/Bandeiras.png` sem duplicar a textura.
- Seleção: imediata, com glow apenas na bandeira ativa e tooltip junto ao cursor usando tempo não escalado/posição direta do mouse.
- Atualização em runtime: menus são reconstruídos no idioma escolhido; HUD do barco atualiza em cada frame; prompts, resultados, painel de upgrades e encerramento respondem ao evento central.
- Textos instanciados depois da troca consultam sempre o idioma atual.
- Typewriters recebem a tradução antes de iniciar, preservando o fluxo e as coroutines existentes.
- Fontes: mantidas Kanit e Russo One; os TMP Font Assets são criados com atlas dinâmico, portanto os caracteres PT-BR são populados quando necessários.

## Escopo alterado

- Cenas modificadas: nenhuma.
- Prefabs modificados: nenhum.
- UI gerada em runtime: Main/Pause/Settings, tutoriais, narrativa, resultados do mergulho, upgrades e encerramento.
- HUD/prompts: casco, turbo e interações.
- Regras de compra: apenas mensagens de retorno passaram a usar chaves; valores e lógica não mudaram.

## Auditoria

- Chaves pareadas: 120 PT-BR + 120 EN.
- Paridade de chaves: validada.
- Paridade dos placeholders `{0}`, `{1}`, `{2}`: validada.
- Textos técnicos mantidos sem tradução: nomes de GameObjects, campos do Inspector, Tooltips de desenvolvimento, mensagens de validação de catálogo e Debug.Log; não fazem parte da interface normal do jogador.
- Textos numéricos mantidos neutros: porcentagens, `[ F ]`, dano/vida flutuantes e contadores sem rótulo.

## Testes executados

- `dotnet build Assembly-CSharp.csproj`: sucesso, 0 erros.
- `git diff --check`: sem erros de whitespace (somente aviso de conversão LF/CRLF do Git).
- Auditoria automatizada de chaves e placeholders: sucesso.
- Revisão estática dos textos em scripts, cenas, prefabs e Resources: concluída.
- Play Mode/WebGL: requer validação visual no Unity para confirmar encaixe final e aparência das bandeiras nas resoluções do jogo.

## Catálogo para revisão linguística

| KEY | PT-BR | EN |
|---|---|---|
| `menu.paused` | PAUSADO | PAUSED |
| `menu.continue` | Continuar | Continue |
| `menu.new_game` | Novo jogo | New Game |
| `menu.settings` | Ajustes | Settings |
| `menu.quit` | Sair | Quit |
| `menu.return_island` | Voltar à ilha | Return to Island |
| `menu.return_menu` | Voltar ao menu | Return to Main Menu |
| `common.back` | Voltar | Back |
| `common.cancel` | Cancelar | Cancel |
| `common.confirm` | Confirmar | Confirm |
| `common.understood` | ENTENDI | GOT IT |
| `common.close` | FECHAR | CLOSE |
| `new_game.title` | NOVO JOGO | NEW GAME |
| `new_game.warning` | Iniciar uma nova aventura?<br>O ouro e os upgrades salvos serão substituídos.<br>Esta ação não pode ser desfeita. | Start a new adventure?<br>Your saved gold and upgrades will be replaced.<br>This action cannot be undone. |
| `new_game.start` | Iniciar novo jogo | Start New Game |
| `travel.menu_title` | VOLTAR AO MENU? | RETURN TO MAIN MENU? |
| `travel.island_title` | VOLTAR À ILHA? | RETURN TO THE ISLAND? |
| `travel.dive_warning` | O mergulho será encerrado.<br>Você mantém o ouro já coletado,<br>mas não recebe o baú final ainda fechado. | The dive will end.<br>You will keep the gold you have collected,<br>but not the unopened final chest. |
| `travel.ocean_warning` | Seu ouro e seus upgrades serão mantidos.<br>O barco retornará ao ponto inicial junto à ilha. | Your gold and upgrades will be kept.<br>The boat will return to its starting point by the island. |
| `quit.title` | DESEJA SAIR? | QUIT GAME? |
| `quit.prompt` | Tem certeza que deseja sair? | Are you sure you want to quit? |
| `quit.yes` | Sim, desejo sair | Yes, quit the game |
| `quit.no` | Vou continuar jogando | Keep playing |
| `settings.title` | AJUSTES | SETTINGS |
| `settings.master_volume` | Volume geral | Master Volume |
| `settings.music` | Som de fundo | Music |
| `settings.effects` | Efeitos sonoros | Sound Effects |
| `settings.steering` | Sensibilidade da curva | Steering Sensitivity |
| `settings.language` | Idioma | Language |
| `hud.hull` | CASCO {0}% | HULL {0}% |
| `hud.turbo` | TURBO {0}% | TURBO {0}% |
| `interaction.press_f_or_click` | Pressione F ou clique | Press F or Click |
| `interaction.open_chest` | Abrir baú | Open Chest |
| `interaction.enter_dive` | Entrar na fase de mergulho | Enter Dive |
| `interaction.open_upgrades` | Abrir upgrades do porto | Open Port Upgrades |
| `interaction.interact` | Interagir | Interact |
| `dive_results.completed` | MERGULHO CONCLUÍDO! | DIVE COMPLETE! |
| `dive_results.ended` | FIM DO MERGULHO | DIVE OVER |
| `dive_results.enemies` | Inimigos derrotados: {0}/{1} | Enemies defeated: {0}/{1} |
| `dive_results.small_chest` | Baú Pequeno ×{0} | Small Chest ×{0} |
| `dive_results.final_chest` | Baú Grande ×{0} | Large Chest ×{0} |
| `dive_results.loose_coins` | Moedas soltas | Loose Coins |
| `dive_results.reward` | +{0} ouro | +{0} Gold |
| `dive_results.total` | TOTAL RECEBIDO: {0} OURO | TOTAL RECEIVED: {0} GOLD |
| `dive_results.return_ocean` | VOLTAR AO OCEANO | RETURN TO THE OCEAN |
| `ending.title` | PARABÉNS, CAPITÃO! | CONGRATULATIONS, CAPTAIN! |
| `ending.body` | Você ajudou a transformar a Ilha do Campeche em um lugar cheio de vida, esperança e novos começos.<br>O farol agora brilha por todos que chamam esta ilha de lar. | You helped transform Campeche Island into a place filled with life, hope, and new beginnings.<br>The lighthouse now shines for everyone who calls this island home. |
| `upgrade.title` | UPGRADES DO PORTO | PORT UPGRADES |
| `upgrade.gold` | OURO | GOLD |
| `upgrade.gold_available` | OURO DISPONÍVEL: {0} | GOLD AVAILABLE: {0} |
| `upgrade.repair.insufficient` | Ouro insuficiente para o conserto. | Not enough Gold for repairs. |
| `upgrade.repair.success` | Barco consertado! Casco 100%. Custo: {0} ouro. | Boat repaired! Hull 100%. Cost: {0} Gold. |
| `upgrade.repair.button` | CONSERTAR BARCO — {0} OURO | REPAIR BOAT — {0} GOLD |
| `upgrade.repair.full` | CASCO 100% — SEM REPAROS | HULL 100% — NO REPAIRS NEEDED |
| `upgrade.status.choose` | Escolha um upgrade. Todos os preços são em ouro. | Choose an upgrade. All prices are in Gold. |
| `upgrade.status.completed` | Ilha N4 — jogo concluído! | Island N4 — game complete! |
| `upgrade.status.completed_thanks` | Ilha N4 — jogo concluído! Obrigado por construir sua ilha. | Island N4 — game complete! Thank you for rebuilding your island. |
| `upgrade.error.village_versions` | Configure as quatro versões da ilha antes de comprar. | Configure all four island versions before purchasing. |
| `upgrade.error.maximum` | Nível máximo atingido. | Maximum level reached. |
| `upgrade.error.offer_changed` | A oferta mudou; atualize o painel. | The offer changed; refresh the panel. |
| `upgrade.error.invalid_price` | Preço inválido. | Invalid price. |
| `upgrade.error.insufficient_gold` | Ouro insuficiente. | Not enough Gold. |
| `upgrade.error.in_progress` | Compra em andamento. | Purchase in progress. |
| `upgrade.error.catalog_missing` | Catálogo de upgrades não configurado. | Upgrade catalog is not configured. |
| `upgrade.error.unknown` | Upgrade desconhecido. | Unknown upgrade. |
| `upgrade.purchase.success` | Upgrade adquirido! | Upgrade purchased! |
| `upgrade.unavailable` | Indisponível | Unavailable |
| `upgrade.pier` | PÍER | PIER |
| `upgrade.pier.description` | O primeiro passo para melhorar o comércio marítimo; depois vem nosso farol! | The first step toward improving maritime trade; next comes our lighthouse! |
| `upgrade.maximum_level` | Nível máximo | Maximum level |
| `upgrade.next_level` | Nível {0}: {1} | Level {0}: {1} |
| `upgrade.maximum` | MÁXIMO | MAX |
| `upgrade.buy` | {0} OURO<br>COMPRAR | {0} GOLD<br>BUY |
| `upgrade.island.2` | Mais moradores chegando, as condições da ilha estão melhorando. | More residents are arriving, and conditions on the island are improving. |
| `upgrade.island.3` | A construção do farol será um grande avanço para a ilha. | Building the lighthouse will be a major step forward for the island. |
| `upgrade.island.4` | Acenda a luz do farol e traga segurança para nossa pequena ilha. | Light the lighthouse and bring safety to our small island. |
| `upgrade.island.appearance` | Uma nova aparência para a ilha. | A new look for the island. |
| `upgrade.boat.2` | Casco +15% / Turbo +20% sobre o valor inicial | Hull +15% / Turbo +20% from base values |
| `upgrade.boat.3` | Casco +30% / Turbo +40% sobre o valor inicial | Hull +30% / Turbo +40% from base values |
| `upgrade.diver_health` | +2 pontos de vida. | +2 health points. |
| `upgrade.diver_speed.2` | 15% sobre o valor inicial | 15% above base value |
| `upgrade.diver_speed.3` | 30% sobre o valor inicial | 30% above base value |
| `upgrade.harpoon.2` | Velocidade +2 / Distância +6 | Speed +2 / Range +6 |
| `upgrade.harpoon.3` | Velocidade +4 / Distância +10 / Dano +1 | Speed +4 / Range +10 / Damage +1 |
| `upgrade.harpoon.4` | Projétil duplo | Double projectile |
| `upgrade.percent_initial` | +{0}% sobre o valor inicial | +{0}% above base value |
| `upgrade.name.island` | Ilha | Island |
| `upgrade.name.vessel` | Embarcação | Vessel |
| `upgrade.name.diver_health` | Mergulhador - Vida | Diver - Health |
| `upgrade.name.diver_speed` | Mergulhador - Velocidade | Diver - Speed |
| `upgrade.name.harpoon` | Mergulhador - Arpão | Diver - Harpoon |
| `upgrade.current_level` | {0} \| Atual Nível {1} | {0} \| Current Level {1} |
| `upgrade.boat_label` | BARCO {0} | BOAT {0} |
| `village.thanks.1` | Os moradores agradecem pelo novo píer! Para tornar este lugar seguro diante das grandes embarcações, ainda precisamos evoluir bastante. Nosso objetivo é construir um farol e fazê-lo brilhar. Será um caminho árduo, mas recompensador. | The villagers are grateful for the new pier! To make this place safe for larger vessels, we still have a long way to go. Our goal is to build a lighthouse and make it shine. It will be a difficult journey, but a rewarding one. |
| `village.thanks.2` | A vila está crescendo com sua ajuda. Os moradores agradecem por mais esta melhoria! | The village is growing thanks to your help. The residents are grateful for this latest improvement! |
| `village.thanks.3` | Cada melhoria torna nossa comunidade mais forte. Obrigado por continuar ao nosso lado! | Every improvement makes our community stronger. Thank you for continuing to stand with us! |
| `village.thanks.4` | O farol voltou a brilhar! Toda a vila agradece por tornar este lugar mais seguro. | The lighthouse shines once more! The entire village thanks you for making this place safer. |
| `difficulty.title` | Escolha a dificuldade | Choose Difficulty |
| `difficulty.easy` | Fácil | Easy |
| `difficulty.normal` | Normal | Normal |
| `difficulty.hard` | Difícil | Hard |
| `story.new_life.title` | UMA NOVA VIDA | A NEW LIFE |
| `story.new_life.body` | Um bom lugar para começar uma nova vida como mergulhador profissional, não acha?<br><br>Dizem que estas águas escondem tesouros e riquezas há muito esquecidos. No fundo do oceano há muito a encontrar, mas chegar até lá nem sempre será fácil.<br><br>O que você trouxer do mar pode ajudar este pequeno vilarejo a crescer. Aos poucos, novas pessoas podem chamar a ilha de lar, enquanto você melhora seus equipamentos e ganha experiência como mergulhador.<br><br>A vida por aqui pode ser simples, mas talvez seja justamente esse o encanto. Um barco, o oceano à sua frente e a chance de construir uma nova vida na Ilha do Campeche.<br><br>Só não esqueça que, conforme você avança, as recompensas aumentam, mas os perigos também. | A good place to begin a new life as a professional diver, don't you think?<br><br>They say these waters hide long-forgotten treasures and riches. There is plenty to find beneath the ocean, but reaching it will not always be easy.<br><br>What you bring back from the sea can help this small village grow. In time, more people may come to call the island home while you improve your equipment and gain experience as a diver.<br><br>Life here may be simple, but perhaps that is part of its charm. A boat, the open ocean, and the chance to build a new life on Campeche Island.<br><br>Just remember: as you progress, the rewards grow—but so do the dangers. |
| `tutorial.continue_prompt` | Clique ou pressione WASD / setas para continuar | Click or press WASD / Arrow Keys to continue |
| `tutorial.boat.title` | CONTROLES DO BARCO | BOAT CONTROLS |
| `tutorial.boat.body` | WASD / Setas — mover e virar<br>Shift — turbo<br>F — interagir<br>P — pausar<br>Botão direito do mouse — movimentar a câmera | WASD / Arrow Keys — move and steer<br>Shift — turbo<br>F — interact<br>P — pause<br>Right mouse button — move the camera |
| `tutorial.dive.title` | CONTROLES DO MERGULHO | DIVING CONTROLS |
| `tutorial.dive.body` | WASD / Setas — nadar<br>Shift — dash<br>Clique esquerdo / segurar — atacar<br>F — interagir<br>P — pausar<br><br>As profundezas do mar guardam muitos desafios. Alguns bichos são mais hostis, outros mais astutos, mas cada mergulho também traz novas descobertas. Mantenha-se atento, explore com coragem e aproveite tudo o que o oceano tem a oferecer. | WASD / Arrow Keys — swim<br>Shift — dash<br>Left-click / hold — attack<br>F — interact<br>P — pause<br><br>The depths hold many challenges. Some creatures are more hostile, others more cunning, but every dive also brings new discoveries. Stay alert, explore bravely, and enjoy everything the ocean has to offer. |
| `rescue.title` | DE VOLTA AO ESTALEIRO | BACK AT THE SHIPYARD |
| `rescue.first_free` | Destruir o barco custa mais do que mantê-lo em boas condições, então procure deixar a manutenção em dia. Desta vez, como sua missão é nobre e ajuda o vilarejo a crescer, o primeiro conserto fica por conta da vila. | Replacing a boat costs far more than keeping it in good condition, so remember to stay on top of maintenance. This time, because your mission is helping the village grow, the villagers will cover the repairs. |
| `rescue.discounted` | Conserto após o naufrágio: {0} ouro.<br>Valor debitado: {1} ouro.<br><br>A vila concedeu um desconto porque sabe que suas finanças não andam boas. A vida no mar não é fácil, mas pode ser muito recompensadora conforme você adquire experiência. | Shipwreck repair: {0} Gold.<br>Amount charged: {1} Gold.<br><br>The village offered you a discount because times are tough. Life at sea is not easy, but experience can make it very rewarding. |
| `rescue.charged` | {0}<br><br>Conserto após o naufrágio: {1} ouro.<br>Valor debitado: {2} ouro. | {0}<br><br>Shipwreck repair: {1} Gold.<br>Amount charged: {2} Gold. |
| `rescue.random.1` | O mar não está pra peixe… e hoje também não estava pra barco! Respire fundo: amanhã a pescaria de tesouros continua. | The sea was not feeling generous today—and neither was your boat. Take a breath: the treasure hunt continues tomorrow. |
| `rescue.random.2` | Você foi conferir o fundo do mar levando o barco inteiro? Entusiasmo não falta! Já está tudo pronto para outra aventura. | Did you decide to inspect the seabed with the entire boat? No shortage of enthusiasm! Everything is ready for another adventure. |
| `rescue.random.3` | Até o melhor pescador já voltou ao píer com mais água no barco do que peixe no balde. Bora tentar de novo! | Even the finest sailor has returned to the pier with more water in the boat than fish in the hold. Ready to try again? |
| `rescue.random.4` | Seu barco confundiu a profissão e quis virar submarino. Conversamos com ele: agora é navegar e deixar o mergulho com você! | Your boat mistook itself for a submarine. We had a word with it: you do the diving, it does the sailing. |
| `rescue.random.5` | O mar cobrou um banho, mas não levou sua coragem. Sacuda a água das botas: ainda tem muito tesouro esperando! | The sea claimed a soaking, but not your courage. Shake the water from your boots—there is still plenty of treasure waiting. |
| `rescue.random.6` | Dizem que quem procura acha. Você achou uma pedra! Na próxima, vamos tentar achar um baú, combinado? | They say those who seek shall find. You found a rock! Let us aim for a treasure chest next time. |
| `rescue.random.7` | Calma, capitão! Isso não foi naufrágio, foi uma visita técnica ao fundo do mar. O estaleiro já liberou sua próxima tentativa! | Easy, Captain! That was not a shipwreck, just a technical inspection of the seabed. The shipyard has cleared you for another attempt. |
| `rescue.random.8` | Se afundar desse experiência, você já era almirante! Levante a cabeça: o fundo do mar ainda reserva grandes aventuras. | If sinking earned experience, you would already be an admiral. Keep your head up—the ocean floor still holds great adventures. |

