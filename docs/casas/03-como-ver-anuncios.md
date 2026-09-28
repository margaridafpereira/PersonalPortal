# Radar de anúncios: como ver os anúncios sem scraping

Atualizado a 28/09/2026.

## O que é scraping e o que não é

- **Scraping** é um programa nosso visitar sozinho os portais (de hora a hora, por exemplo), copiar os anúncios para a nossa base de dados e mostrá-los na nossa plataforma. **É isto que não fazemos.** Ver [02-questoes-legais.md](02-questoes-legais.md).
- **Não é scraping:** a plataforma guardar as tuas pesquisas e abrir os portais já filtrados, receber alertas que tu pediste, ou guardares um anúncio de que gostaste.

Continuas a ver os anúncios do Idealista, Imovirtual, Casa Yes e dos outros portais. O que muda é **onde** os vês e **quem** os traz.

### "E se a plataforma mostrar os anúncios mas o clique redirecionar para o portal?"

Não resolve. O problema não está no clique: para mostrar a lista (título, preço, foto), a plataforma tem de **ir buscar e guardar** esses dados, e é essa recolha que os termos proíbem. No caso C-762/19, o motor de pesquisa Melons também redirecionava os utilizadores para o site original, e o Tribunal considerou mesmo assim que havia extração e reutilização da base de dados.

O que funciona é o nível 1: a plataforma guarda **a pesquisa**, e não os anúncios, e o link abre o portal já filtrado.

## As 4 formas de trazer anúncios

### Nível 1: pesquisas guardadas com links diretos (V1)
Defines uma vez os critérios: tipo (casa, apartamento, terreno), comprar ou arrendar, zonas, preço máximo, tipologia e área mínima. A plataforma gera um botão por portal que **abre a pesquisa já filtrada** no site do portal.

```
Pesquisa "T2 Lisboa até 300k"
  [Ver no Idealista]  [Ver no Imovirtual]  [Ver no Casa Yes]  [Ver no Casa Sapo]  [Ver no Supercasa]
```

- Não se copia nada: é só um link, como um favorito inteligente.
- É útil porque cada portal tem filtros e URLs diferentes, e isto poupa repetir a pesquisa em cinco sites.
- Cada portal precisa de um "tradutor" dos critérios para o formato do seu URL (padrões abaixo).

### Nível 2: guardar anúncios (V1)
Quando encontras um anúncio de que gostas, guardas o link e preenches o preço, a área, a tipologia e a freguesia.

A plataforma acrescenta:
- a comparação com a mediana do INE na freguesia ("13% acima da mediana");
- o custo real: IMT, Imposto do Selo, isenção IMT Jovem, garantia pública e prestação estimada;
- o risco de inundação e incêndio (GEO API PT);
- notas pessoais, estado (visto, visitado, descartado) e histórico de preços sempre que atualizas o preço.

Em evolução, um bookmarklet ou uma extensão do browser pode preencher os campos a partir da página que estás a ver, com um clique teu. Continua a ser uso pessoal, mas os termos do Idealista proíbem até a cópia manual. Só faz sentido para uso privado.

### Nível 3: alertas por email (V2)
Crias alertas nos próprios portais, e eles enviam-te os anúncios novos e as descidas de preço por email. A plataforma lê só essa pasta da tua caixa e cria uma lista cronológica com a deteção de repetidos.
- Só para uso privado, sem republicar.

### Nível 4: API oficial (se for aprovada)
- **Idealista:** pedido em <https://developers.idealista.com/access-request>. Com a aprovação, os anúncios aparecem dentro da plataforma, dentro das regras.
- **Casa Yes:** portal da APEMIP, só com anúncios de profissionais (mais de 130 mil imóveis). Vale a pena perguntar se tem API ou programa de parceiros.

## Seguir os anúncios de que gostas (favoritos)

Objetivo: para cada anúncio marcado com "gosto", saber se o **preço desceu**, se **subiu** e se o **anúncio desapareceu** (vendido ou retirado).

Cada favorito guarda um histórico de eventos:

| Evento | Exemplo de mensagem |
|---|---|
| `preco_desceu` | "T2 Arroios: 320 000 € → 305 000 € (−4,7%)" |
| `preco_subiu` | "Terreno Sintra: 90 000 € → 95 000 €" |
| `desapareceu` | "Moradia Cascais saiu do portal ao fim de 63 dias" |
| `reapareceu` | "Voltou a ser anunciado, agora a 298 000 €" |

Métricas derivadas: dias no mercado, número de descidas e descida total em %.

**De onde vêm as alterações, da mais segura para a menos segura:**

| Opção | Deteta | Risco | Nota |
|---|---|---|---|
| A. Emails de favoritos dos portais | Descidas; às vezes a remoção | Nenhum | Vários portais avisam quando um favorito baixa de preço. **Confirmar quais avisam, e se avisam também de subidas e remoções** |
| B. Atualização quando visitas | Tudo | Nenhum | Ao abrires o anúncio, uma extensão do browser atualiza o preço na plataforma. Só apanha o que visitas |
| C. Verificação leve e automática | Tudo | **Zona cinzenta** | 1 pedido por favorito por dia, só para os teus favoritos. Os termos do Idealista proíbem "monitorizar"; por isso fica **desligada para o Idealista** e só se liga nos portais cujos termos o permitam |

Proposta: começar por A + B e decidir C portal a portal, depois de ler os termos de cada um.

## Padrões de URL de pesquisa (a validar no browser)

Testados a 28/09/2026 com `curl`:

| Portal | Exemplo | Resultado do teste |
|---|---|---|
| Imovirtual | `https://www.imovirtual.com/pt/resultados/comprar/apartamento/lisboa/lisboa?priceMax=300000` | 200 ✅ |
| Imovirtual (terrenos) | `https://www.imovirtual.com/pt/resultados/comprar/terreno/lisboa/sintra` | 200 ✅ |
| Casa Yes (terrenos) | `https://casayes.pt/en/comprar/terreno/lisboa/cascais` | Visto em pesquisa |
| Idealista | `https://www.idealista.pt/comprar-casas/lisboa/com-preco-max_300000,t2/` | 403, bloqueio anti-bot; validar no browser |
| Idealista (terrenos) | `https://www.idealista.pt/comprar-terrenos/sintra/` | 403; validar no browser |
| Casa Sapo | `https://casa.sapo.pt/comprar-apartamentos/lisboa/` | 504; validar no browser |
| Supercasa | `https://supercasa.pt/comprar-casas/lisboa` | 403; validar no browser |

O 403 é esperado: os portais bloqueiam pedidos que não vêm de um browser. Não é um problema para os links, porque quem os abre és tu, no teu browser.

## Tipos de imóvel suportados
Apartamento, moradia, terreno (urbano e rústico), quinta e herdade, e loja ou armazém (evolução). Cada tipo tem os seus critérios próprios: para terrenos contam a área do terreno, o tipo (urbano ou rústico) e a viabilidade de construção.

## Fontes
- [Casa Yes: portal da APEMIP (4gnews)](https://4gnews.pt/casa-yes-e-o-primeiro-portal-imobiliario-com-inteligencia-artificial-em-portugal/)
- [Casa Yes: terrenos em Cascais](https://casayes.pt/en/comprar/terreno/lisboa/cascais)
- [Idealista: pedido de acesso à API](https://developers.idealista.com/access-request)
