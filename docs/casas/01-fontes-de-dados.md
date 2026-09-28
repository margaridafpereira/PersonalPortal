# Radar de casas: fontes de dados

Pesquisa e testes feitos a 28/09/2026.

## Resumo

| Fonte | Tipo de dado | Acesso | Estado |
|---|---|---|---|
| **INE: API JSON** | Preço mediano de venda e de renda por m², por freguesia | API pública, sem registo | ✅ **Testada, funciona** |
| **GEO API PT** | Coordenadas → freguesia, concelho, distrito | API pública, 900 pedidos por 15 min | ✅ **Testada, funciona** |
| **DGT: CAOP 2025 (OGC API)** | Limites oficiais das freguesias (polígonos) | API OGC e download | Documentado, não testado |
| **Idealista: API oficial** | Anúncios | Pedido de acesso, sujeito a aprovação | A pedir |
| Idealista, Imovirtual, Casa Sapo, Supercasa (sites) | Anúncios | Scraping | ⛔ **Não usar**; ver [02-questoes-legais.md](02-questoes-legais.md) |
| Alertas por email dos portais | Anúncios que o próprio utilizador recebe | Caixa de email do utilizador | Opção a estudar |
| e-Leilões (OSAE) | Imóveis penhorados em leilão | Web | A estudar (termos de uso) |
| Portais de imóveis da banca | Imóveis de bancos | Web | A estudar (termos de uso) |
| IHRU Arrenda | Concursos de arrendamento público | Web | A estudar |

---

## 1. INE: API JSON ✅

Gratuita e sem registo. Manual oficial: [API JSON: manual do utilizador](https://www.ine.pt/ngt_server/attachfileu.jsp?look_parentBoui=322762582&att_display=n&att_download=y).

**Endpoints:**
- Metadados: `https://www.ine.pt/ine/json_indicador/pindicaMeta.jsp?varcd={codigo}&lang=PT`
- Dados: `https://www.ine.pt/ine/json_indicador/pindica.jsp?op=2&varcd={codigo}&Dim1={periodo}&Dim2={geo}&lang=PT`
- Catálogo completo (XML, cerca de 0,5 MB): `https://www.ine.pt/ine/xml_indic.jsp?opc=3&lang=PT`

**Indicadores relevantes:**

| Código | Indicador | Periodicidade | Nível geográfico | Último período |
|---|---|---|---|---|
| `0012234` | Valor mediano das vendas nos últimos 12 meses (€/m²), por categoria (total, novos, existentes) | Trimestral | Freguesia* | 1.º trim. 2026 |
| `0012235` | Idem, só apartamentos | Trimestral | Freguesia* | — |
| `0012241` | Idem, por tipologia (T0, T1…) | Trimestral | Freguesia* | — |
| `0012600` | Valor mediano das rendas de novos contratos (€/m²) | Anual | Freguesia | 2024 |
| `0012571` | Idem | Trimestral | NUTS III | 1.º trim. 2025 (confirmar se continua a ser publicado) |

\* Há freguesias na Área Metropolitana do Porto, na Grande Lisboa, na Península de Setúbal, no Algarve e nos municípios com mais de 100 mil habitantes. No resto do país os dados param no concelho.

**Teste real** (`0012234`, 1.º trimestre de 2026, `Dim1=S5A20261`), com 2 793 linhas devolvidas:

| Local | Código geo | Total €/m² | Novos | Existentes |
|---|---|---|---|---|
| Portugal | `PT` | 2 168 | 2 402 | 2 108 |
| Lisboa (concelho) | `1A01106` | 5 082 | 6 226 | 4 896 |
| Arroios (freguesia) | `1A0110656` | 4 776 | 4 488 | 4 828 |
| Porto (concelho) | `11A1312` | 3 510 | 3 904 | 3 292 |
| Braga (concelho) | `1120303` | 2 100 | 2 375 | 2 012 |

Formato de cada linha:
```json
{"geocod":"1A0110656","geodsg":"Arroios","dim_3":"H1","dim_3_t":"Total","valor":"4776"}
```

**Notas técnicas:**
- Os códigos de período seguem o formato `S5A{ano}{trimestre}` (trimestral) ou `S7A{ano}` (anual).
- A dimensão 3 usa `H1` (total), `H11` (novos) e `H12` (existentes). Um código inválido devolve `Sucesso.Falso` com uma mensagem de erro.
- Os códigos geográficos seguem NUTS 2024, e não os códigos DICOFRE da CAOP. **É preciso uma tabela de correspondência INE ↔ CAOP** para ligar os valores ao mapa.
- Na rede da empresa, o `curl` precisa de `--ssl-no-revoke`. Em produção não será necessário.

**Uso no portal:** um job trimestral guarda os valores em `mercado_freguesia(periodo, geocod, categoria, eur_m2)`. Cada imóvel mostra então "este T2 está a 5 400 €/m², 13% acima da mediana de Arroios".

## 2. GEO API PT ✅

- Documentação: <https://geoapi.pt/docs/>; código aberto: <https://github.com/Moser-ss/geoapi.pt>
- Limite: 900 pedidos por 15 minutos por IP.
- **Teste real:** `GET https://geoapi.pt/gps/38.7223,-9.1393?json=1`
  ```json
  {"distrito":"Lisboa","concelho":"Lisboa","freguesia":"Arroios","altitude_m":61,
   "perigo_inundacao":"Nulo / Informação Inexistente","perigo_incendio":"Nulo","SEC":"009","SS":"04"}
  ```
- Um bónus útil para casas: devolve o **perigo de inundação e de incêndio** e a secção e subsecção estatística.
- Uso: coordenadas do imóvel → freguesia → valor do INE.

## 3. DGT: CAOP 2025

- OGC API das freguesias: <https://ogcapi.dgterritorio.gov.pt/collections/freguesias>
- Downloads e metadados: [SNIG: CAOP 2025](https://snig.dgterritorio.gov.pt/rndg/srv/api/records/198497815bf647ecaa990c34c42e932e/formatters/snig-view) · <https://www.dgterritorio.gov.pt/dados-abertos>
- Uso: importar os polígonos para o PostGIS uma vez por ano, para desenhar o mapa com a cor do €/m². Permite fazer a correspondência coordenadas → freguesia localmente, sem depender da GEO API.

## 4. Anúncios

### Idealista: API oficial
- Pedido de acesso em <https://developers.idealista.com/access-request>. É preciso descrever o projeto e o acesso depende de aprovação. Se for aceite, recebe-se uma chave, um *secret* e a documentação.
- **Ação:** pedir o acesso, descrevendo o projeto como pessoal e de portefólio, sem fins comerciais. É o único caminho legítimo para dados do Idealista.

### Alertas por email (a estudar)
Os portais enviam por email os novos anúncios que correspondem às pesquisas guardadas. Ler **a caixa de email do próprio utilizador** (Gmail API, ou uma pasta IMAP dedicada) permitiria:
- registar cada anúncio com data, preço e link;
- detetar descidas de preço quando o portal envia o alerta de "baixou";
- não fazer nenhum pedido aos portais.

Isto ainda é reutilização de conteúdo dos portais. É aceitável para **uso pessoal e privado**, mas não para republicar num site público. Ver [02-questoes-legais.md](02-questoes-legais.md).

### Introdução manual
O utilizador cola o link e preenche o preço, a área e a tipologia. É a opção mais simples e sem riscos para a primeira versão. O portal guarda o histórico sempre que o utilizador atualiza o preço.

### Alternativas públicas
- **e-Leilões** (<https://www.e-leiloes.pt/>): leilões de bens penhorados geridos pela OSAE. Os imóveis são 64% do que é licitado. Os dados são de natureza pública, mas os termos de uso ainda têm de ser verificados. Na rede da empresa, o certificado do site falhou a validação.
- **Portais de imóveis da banca**: Caixa Imobiliário, M Imóveis (Millennium) e outros. Lista em [NValores](https://www.nvalores.pt/imoveis-dos-bancos/). Verificar os termos de cada um.
- **IHRU Arrenda** (<https://ihruarrenda.portaldahabitacao.pt/>): concursos de arrendamento a custos controlados. Liga-se ao radar de apoios.

## 5. Dados a calcular com o radar de apoios
- **IMT e Imposto do Selo** para cada imóvel, com a isenção IMT Jovem se o perfil for elegível.
- **Elegibilidade para a garantia pública** (imóvel até 450 000 €, idade até 35 anos).
- **Prestação estimada** do crédito, com a Euribor como parâmetro.

## Fontes
- [INE: API de dados](https://www.ine.pt/xportal/xmain?xpid=INE&xpgid=ine_api_v2&xlang=pt)
- [INE: Estatísticas de preços da habitação ao nível local, 4.º trim. 2025](https://www.ine.pt/ngt_server/attachfileu.jsp?look_parentBoui=789055201&att_display=n&att_download=y)
- [GEO API PT: documentação](https://geoapi.pt/docs/)
- [Idealista: pedido de acesso à API](https://developers.idealista.com/access-request)
- [e-Leilões: vendas em 2024 (ECO)](https://eco.sapo.pt/2025/02/24/vendas-de-bens-penhorados-na-plataforma-e-leiloes-atingem-629-milhoes-em-2024/)
