# Carro: fontes e regras

Pesquisa e testes feitos a 28/09/2026.

## Preços dos combustíveis: DGEG ✅ testada

API pública do portal [Preços dos Combustíveis Online](https://precoscombustiveis.dgeg.gov.pt/), da Direção-Geral de Energia e Geologia. Os preços são comunicados pelos próprios postos.

| Endpoint | Para quê |
|---|---|
| `api/PrecoComb/GetDistritos` | Lista de distritos com o id (Porto = 13) |
| `api/PrecoComb/GetMunicipios?idDistrito=13` | Concelhos do distrito com o id (Valongo = 196) |
| `api/PrecoComb/GetTiposCombustiveis` | Tipos de combustível com o id |
| `api/PrecoComb/PesquisarPostos?idsTiposComb=2101&idDistrito=13&idsMunicipios=196&qtdPorPagina=200&pagina=1` | Postos e preços |

**Ids dos combustíveis** (os mesmos valores do enum `Combustivel`):

| Id | Combustível |
|---|---|
| 2101 | Gasóleo simples |
| 2105 | Gasóleo especial |
| 3201 | Gasolina simples 95 |
| 3205 | Gasolina especial 95 |
| 3400 | Gasolina 98 |
| 1120 | GPL Auto |

**Formato de cada posto:** `Nome`, `Marca`, `Morada`, `Localidade`, `Municipio`, `Distrito`, `Combustivel`, `Preco` (texto, ex.: `"2,129 €"`), `DataAtualizacao`, `Latitude`, `Longitude`.

**Teste real** (Valongo, gasóleo simples): 25 postos; o mais barato a 2,129 € (Intermarché Ermesinde), com média de 2,245 €.

**Notas:**
- Nem todos os postos atualizam o preço todos os dias; o campo `DataAtualizacao` mostra quando foi a última vez.
- Não há documentação oficial da API; os endpoints foram confirmados a partir de projetos da comunidade ([gas-prices-finder](https://github.com/luisalvesntc-hub/gas-prices-finder), [ha-precoscombustiveis](https://github.com/netsoft-ruidias/ha-custom-component-precoscombustiveis)) e testados.
- Cache: lista de concelhos 24 h, preços 1 h.

## Inspeção periódica obrigatória (IPO)

| Categoria | Inspeções |
|---|---|
| Ligeiros de passageiros | aos 4 anos da 1.ª matrícula, depois de 2 em 2 anos até aos 8, e a partir daí anual |
| Ligeiros de mercadorias | aos 2 anos, depois anual *(a confirmar)* |

- A data-limite é o aniversário da primeira matrícula. A inspeção pode ser feita **até 3 meses antes** sem mudar o ciclo.
- Coima por atraso: 250 € a 1 250 €.
- Fontes: [gov.pt: levar o carro à inspeção](https://www.gov.pt/servicos/levar-o-carro-a-inspecao), [Controlauto: FAQ](https://controlauto.pt/inspecao-automovel/inspecoes-automoveis-perguntas-frequentes).

## IUC

- Paga-se todos os anos, **durante o mês da matrícula**. O prazo é o último dia desse mês, passando para o dia útil seguinte quando calha ao fim de semana.

## Seguro e revisão

- O utilizador indica uma data de renovação do seguro; o prazo repete-se todos os anos. A revisão é uma data única, indicada à mão.

## Em aberto

- [ ] Confirmar a periodicidade da inspeção dos ligeiros de mercadorias e acrescentar motociclos
- [ ] Feriados nacionais no cálculo do dia útil
- [ ] Postos por distância (as coordenadas já vêm na API) em vez de por concelho

## Acrescentado a 29/09/2026

### Estimativa do IUC (`Iuc.cs`)

Taxas do Código do IUC em vigor em 2024, 2025 e 2026: o OE 2026 (Lei n.º 73-A/2025) não as alterou. Fontes: [DECO PROteste](https://www.deco.proteste.pt/dinheiro/impostos/noticias/tabelas-iuc-quanto-paga) e [impostosobreveiculos.info](https://impostosobreveiculos.info/iuc/imposto-unico-circulacao-iuc-2026/), que coincidem.

- **Categoria B** (ligeiros de passageiros matriculados desde 1/7/2007): (taxa da cilindrada + taxa do CO2) × coeficiente do ano (2007: 1,00; 2008: 1,05; 2009: 1,10; 2010 e seguintes: 1,15). Escalões de CO2 diferentes para NEDC e WLTP.
- Adicional de CO2 para matrículas desde 2017 nos dois escalões mais altos (31,77 € e 63,74 €) e adicional de gasóleo pela cilindrada.
- **Categoria A** (1981 a junho de 2007): tabela por cilindrada e idade. Não inclui o adicional de gasóleo desta categoria (sem fonte confirmada); o portal avisa.
- 100% elétricos da categoria B: isentos. Ligeiros de mercadorias: não estimado (paga-se pelo peso); o portal remete para o Portal das Finanças.
- Dúvida registada: se os adicionais também são multiplicados pelo coeficiente. O portal soma-os sem coeficiente e apresenta o valor como estimativa.

### Carta de condução

Grupo 1 (inclui a categoria B): revalidação aos 30, 40, 50, 60, 65 e 70 anos, depois de 2 em 2 anos; a partir dos 60 com atestado médico; pode pedir-se até 6 meses antes ([IMT](https://www.imt-ip.pt/condutores/informacoes-gerais/quero-ser-condutor/revalidacao-da-carta-de-conducao/)). Com a validade da carta (campo 4b), o portal usa-a; sem ela, estima pela data de nascimento.

### Seguro

O prazo da renovação mostra a seguradora e o valor anual, se estiverem indicados. (Houve um aviso "comparar seguros" 30 dias antes; foi retirado a pedido.)

### Carregamento elétrico (Mobi.E)

Ficheiro público de tarifas da Mobi.E (`https://www.mobie.pt/documents/42032/106470/Tarifas`, CSV com `;`, cerca de 50 mil tomadas), o mesmo que usa o projeto [CargaCerta](https://github.com/rickpsilva/cargacerta). Colunas usadas: `ID`, `UID_TOMADA`, `TIPO_POSTO`, `MUNICIPIO`, `MORADA`, `OPERADOR`, `TARIFA` ("€ 0.04 /min até 45 min"), `POTENCIA_TOMADA`.

O portal compara o **custo do operador do posto (OPC)** para carregar 20 kWh, com o tempo estimado pela potência da tomada (até 50 kW). Não inclui a energia do comercializador (CEME) nem os impostos, que dependem do contrato de cada pessoa. Fica em cache 12 horas.
