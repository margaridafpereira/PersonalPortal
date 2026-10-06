import { useEffect, useState, type FormEvent } from 'react'
import { api, dataCurta, euros, type NovaOperacao, type Operacao, type RelatorioIrs, type TipoAtivo } from '../api'
import { Explicacao } from '../componentes/Explicacao'
import { Icone } from '../componentes/Icone'
import { t } from '../i18n'
import { ImportarOperacoes } from './ImportarOperacoes'

const eur2 = new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' })
const tiposOperacao = () => ({ Compra: t('Compra', 'Buy'), Venda: t('Venda', 'Sell'), Dividendo: t('Dividendo', 'Dividend') })
const hoje = new Date()
const anoPorOmissao = hoje.getMonth() < 6 ? hoje.getFullYear() - 1 : hoje.getFullYear()

export function InvestimentosPagina() {
  const [separador, setSeparador] = useState<'relatorio' | 'operacoes'>('relatorio')
  const [operacoes, setOperacoes] = useState<Operacao[] | null>(null)

  const carregar = () => api.operacoes().then(setOperacoes)
  useEffect(() => { carregar() }, [])

  return (
    <>
      <header className="pagina-topo">
        <div>
          <h1>{t('IRS de investimentos', 'Investment taxes (IRS)')}</h1>
          <p className="suave">{t('Mais-valias e dividendos de corretoras estrangeiras, convertidos ao câmbio do Banco de Portugal e prontos para o Anexo J.', 'Capital gains and dividends from foreign brokers, converted at Banco de Portugal rates and ready for Annex J.')}</p>
        </div>
      </header>

      <p className="aviso pequeno">
        <Icone nome="alerta" tamanho={14} />
        {t('Estimativa indicativa. Confirma os valores antes de entregar a declaração; em caso de dúvida, fala com um contabilista.', 'Indicative estimate. Check the figures before filing; if in doubt, talk to an accountant.')}
      </p>

      <div className="separadores" role="tablist">
        <button role="tab" aria-selected={separador === 'relatorio'} onClick={() => setSeparador('relatorio')}>
          <Icone nome="escudo" tamanho={18} /> {t('Anexo J', 'Annex J')}
        </button>
        <button role="tab" aria-selected={separador === 'operacoes'} onClick={() => setSeparador('operacoes')}>
          <Icone nome="calendario" tamanho={18} /> {t('Operações', 'Transactions')} <span className="contador">{operacoes?.length ?? 0}</span>
        </button>
      </div>

      {separador === 'relatorio' && <Relatorio operacoes={operacoes} aoImportar={() => setSeparador('operacoes')} />}
      {separador === 'operacoes' && operacoes && <Operacoes operacoes={operacoes} aoMudar={carregar} />}
    </>
  )
}

/** Anos com algo para declarar: vendas (mais-valias) ou dividendos. As compras sozinhas não entram no Anexo J. */
function anosComRendimentos(operacoes: Operacao[]) {
  const anos = new Map<number, Set<string>>()
  for (const o of operacoes)
    if (o.tipo !== 'Compra') {
      const ano = Number(o.momento.slice(0, 4))
      anos.set(ano, (anos.get(ano) ?? new Set()).add(o.tipo === 'Venda' ? 'vendas' : 'dividendos'))
    }
  return [...anos].sort((a, b) => b[0] - a[0]).map(([ano, tipos]) => ({ ano, tipos: [...tipos] }))
}

function Relatorio({ operacoes, aoImportar }: { operacoes: Operacao[] | null; aoImportar: () => void }) {
  const vazio = operacoes?.length === 0
  const comRendimentos = anosComRendimentos(operacoes ?? [])
  // Os últimos seis anos e todos os anos com operações (um histórico antigo tem de caber na lista).
  const anos = [...new Set([...Array.from({ length: 6 }, (_, i) => hoje.getFullYear() - i), ...comRendimentos.map(a => a.ano)])].sort((a, b) => b - a)
  const [ano, setAno] = useState(anoPorOmissao)
  const [relatorio, setRelatorio] = useState<RelatorioIrs | null>(null)
  const [erro, setErro] = useState<{ ano: number; mensagem: string } | null>(null)

  useEffect(() => {
    api.relatorio(ano).then(setRelatorio, (e: Error) => setErro({ ano, mensagem: e.message }))
  }, [ano])

  // Enquanto o ano novo carrega, não mostrar o relatório do ano anterior.
  const r = relatorio?.ano === ano ? relatorio : null

  if (vazio)
    return (
      <div className="vazio">
        <p>{t('Ainda não tens operações.', 'No transactions yet.')}</p>
        <button onClick={aoImportar}>{t('Importar da corretora', 'Import from your broker')}</button>
      </div>
    )

  return (
    <>
      <div className="barra-acoes">
        <label className="linha">{t('Rendimentos de', 'Income for')}
          <select value={ano} onChange={e => setAno(Number(e.target.value))} style={{ width: 'auto' }}>
            {anos.map(a => <option key={a}>{a}</option>)}
          </select>
        </label>
        <span className="suave pequeno">{t(`Declaração entregue entre abril e junho de ${ano + 1}`, `Return filed between April and June ${ano + 1}`)}</span>
      </div>

      {r && r.maisValias.length === 0 && r.dividendos.length === 0 && (
        <div className="aviso pequeno">
          <Icone nome="alerta" tamanho={14} />
          <div>
            <p>{t(`Não há nada a declarar em ${ano}: o Anexo J de um ano só leva as vendas e os dividendos desse ano (as compras só servem para calcular o custo quando vendes).`, `Nothing to declare for ${ano}: a year's Annex J only covers that year's sales and dividends (purchases only set the cost when you sell).`)}</p>
            {comRendimentos.length > 0 && (
              <p className="linha">
                {t('Anos com rendimentos:', 'Years with income:')}
                {comRendimentos.map(a => (
                  <button key={a.ano} className="ligacao pequeno" onClick={() => setAno(a.ano)}>{a.ano} ({a.tipos.map(tipo => tipo === 'vendas' ? t('vendas', 'sales') : t('dividendos', 'dividends')).join(t(' e ', ' and '))})</button>
                ))}
              </p>
            )}
          </div>
        </div>
      )}

      {erro?.ano === ano && !r ? <p className="erro">{t('Não foi possível calcular o relatório:', 'Could not calculate the report:')} {erro.mensagem}</p>
        : !r ? <p>{t('A calcular (a obter os câmbios do Banco de Portugal)…', 'Calculating (fetching Banco de Portugal exchange rates)…')}</p> : (
        <>
          <div className="indicadores resumo-irs">
            <div className={`indicador ${r.saldoMaisValias >= 0 ? 'tom-positivo' : 'tom-aviso'}`}>
              <strong>{euros(r.saldoMaisValias)}</strong><span>{t('saldo de mais-valias', 'net capital gains')}</span>
              <Explicacao titulo={t('O que é?', 'What is it?')}>
                <p>{t('Soma, em euros, de todas as vendas do ano: valor de venda − valor de compra − despesas. A perda num título compensa o ganho noutro.', 'The sum, in euros, of every sale in the year: sale value − purchase value − expenses. A loss on one holding offsets a gain on another.')}</p>
                <p>
                  {t('Se o saldo for positivo, paga 28% (taxa autónoma). Em alternativa podes optar pelo englobamento: somar este saldo aos outros rendimentos e pagar à taxa do teu escalão, o que compensa se essa taxa for inferior a 28%. O portal não simula o englobamento.',
                    'A positive balance pays 28% (flat rate). Alternatively you can opt to aggregate it with your other income and pay your bracket rate, which pays off if that rate is below 28%. The portal does not simulate aggregation.')}
                </p>
                <p>{t('Um saldo negativo não paga nada; só pode ser reportado aos anos seguintes se optares pelo englobamento.', 'A negative balance pays nothing; it can only be carried forward if you opt for aggregation.')}</p>
              </Explicacao>
            </div>
            <div className="indicador">
              <strong>{euros(r.dividendosBrutos)}</strong><span>{t('dividendos brutos', 'gross dividends')}</span>
              <Explicacao titulo={t('O que é?', 'What is it?')}>
                <p>
                  {t('O valor dos dividendos antes de qualquer imposto, convertido para euros ao câmbio do dia do pagamento. Declara-se o bruto, não o que chegou à conta: o imposto retido lá fora entra à parte, como crédito.', 'Dividends before any tax, converted to euros at the payment date rate. You declare the gross amount, not what reached your account: tax withheld abroad goes in separately, as a credit.')}
                </p>
              </Explicacao>
            </div>
            <div className="indicador">
              <strong>{euros(r.retencaoEstrangeiro)}</strong><span>{t('retido no estrangeiro que conta', 'foreign tax that counts')}</span>
              <Explicacao>
                <p>
                  {t('Quando recebes dividendos de uma empresa estrangeira, o país dela fica logo com uma parte (retenção na fonte). Para não pagares duas vezes, esse valor abate ao imposto português: é o crédito por dupla tributação.', 'When you get dividends from a foreign company, its country keeps a share straight away (withholding tax). So you do not pay twice, that amount is deducted from Portuguese tax: the double-taxation credit.')}
                </p>
                <p>
                  {t('Mas só abate até à taxa da convenção entre Portugal e esse país. Nos EUA é 15%: se te retiveram 30% (acontece quando a corretora não tem o teu formulário W-8BEN), os outros 15% não abatem ao IRS. Esse excesso só se recupera pedindo-o às finanças dos EUA; para o evitar, confirma na corretora que o W-8BEN está preenchido.', 'But only up to the treaty rate between Portugal and that country. For the US it is 15%: if 30% was withheld (it happens when your broker does not have your W-8BEN form), the other 15% is not deducted from IRS. You can only get that back from the US tax authority; to avoid it, check your W-8BEN is filed with your broker.')}
                </p>
                {r.dividendos.some(d => d.retencaoNaoCreditada > 0) && (
                  <p><strong>{t(`Este ano, ${euros(r.dividendos.reduce((s, d) => s + d.retencaoNaoCreditada, 0))} foram retidos acima da convenção e não contam.`, `This year, ${euros(r.dividendos.reduce((s, d) => s + d.retencaoNaoCreditada, 0))} was withheld above the treaty rate and does not count.`)}</strong></p>
                )}
              </Explicacao>
            </div>
            <div className="indicador tom-aviso">
              <strong>{euros(r.impostoMaisValias + r.impostoDividendos)}</strong><span>{t('imposto estimado (28%)', 'estimated tax (28%)')}</span>
              <Explicacao titulo={t('Como é calculado?', 'How is it calculated?')}>
                <p>
                  {t(`28% do saldo positivo de mais-valias (${euros(r.impostoMaisValias)}) + 28% dos dividendos brutos menos o imposto já pago lá fora, país a país (${euros(r.impostoDividendos)}).`, `28% of the positive capital-gains balance (${euros(r.impostoMaisValias)}) + 28% of gross dividends minus tax already paid abroad, country by country (${euros(r.impostoDividendos)}).`)}
                </p>
                <p>{t('É uma estimativa: a conta final é feita pelas Finanças, com todos os teus rendimentos e as opções que escolheres na declaração.', 'This is an estimate: the final figure is worked out by the tax authority, with all your income and the options you choose on the return.')}</p>
              </Explicacao>
            </div>
          </div>

          {r.avisos.length > 0 && (
            <div className="aviso pequeno avisos">
              <Icone nome="alerta" tamanho={14} />
              <ul>{r.avisos.map(a => <li key={a}>{a}</li>)}</ul>
            </div>
          )}

          <section className="cartao tabela-cartao">
            <h2>{t('Quadro 9.2A · Mais-valias', 'Table 9.2A · Capital gains')} <span className="suave pequeno">{t('(alienação onerosa de partes sociais e outros valores mobiliários)', '(sale of shares and other securities)')}</span></h2>
            <Explicacao titulo={t('Como ler esta tabela', 'How to read this table')}>
              <p>
                {t('Cada linha é uma venda casada com a compra de onde vieram os títulos. Pelo método FIFO, vendem-se primeiro os títulos comprados há mais tempo; se uma venda levar títulos de duas compras, aparece em duas linhas. É assim que se preenche o quadro.',
                  'Each line is a sale matched to the purchase the shares came from. Under FIFO, the oldest shares are sold first; if a sale takes shares from two purchases, it shows as two lines. That is how the table is filled in.')}
              </p>
              <p>
                {t('Os valores estão em euros, ao câmbio de referência do Banco de Portugal no dia de cada operação (o da compra para a aquisição, o da venda para a realização). As despesas são as comissões da compra e da venda, na proporção dos títulos vendidos.',
                  'Amounts are in euros, at the Banco de Portugal reference rate on the day of each transaction (purchase date for the cost, sale date for the proceeds). Expenses are the purchase and sale fees, in proportion to the shares sold.')}
              </p>
              <p>
                <strong>{t('País', 'Country')}</strong>: {t('código numérico do país do emitente, tirado das duas primeiras letras do ISIN; confirma na tabela da declaração.', 'numeric code of the issuer country, taken from the first two letters of the ISIN; check the table in the return.')}
                {' '}<strong>{t('Código', 'Code')}</strong>: {t('G01 para ações, G20 para ETF e fundos.', 'G01 for shares, G20 for ETFs and funds.')}
                {' '}<strong>&lt; {t('365 dias', '365 days')}</strong>: {t('títulos detidos menos de um ano. Se o teu rendimento coletável total chegar ao último escalão do IRS, o ganho destes títulos tem de ser englobado.', 'shares held under a year. If your total taxable income reaches the top IRS bracket, gains on these must be aggregated.')}
              </p>
            </Explicacao>
            {r.maisValias.length === 0 ? <p className="suave">{t(`Sem vendas em ${r.ano}.`, `No sales in ${r.ano}.`)}</p> : (
              <div className="tabela-rolar">
                <table>
                  <thead>
                    <tr>
                      <th>{t('País', 'Country')}</th><th>{t('Código', 'Code')}</th><th>{t('Título', 'Holding')}</th>
                      <th>{t('Realização', 'Sale')}</th><th className="num">{t('Valor', 'Value')}</th>
                      <th>{t('Aquisição', 'Purchase')}</th><th className="num">{t('Valor', 'Value')}</th>
                      <th className="num">{t('Despesas', 'Expenses')}</th><th className="num">{t('Resultado', 'Result')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {r.maisValias.map((l, i) => (
                      <tr key={i}>
                        <td title={l.pais}>{l.codigoPais}</td>
                        <td>{l.codigo}</td>
                        <td><span title={l.ativo}>{l.nome}</span> <span className="suave pequeno">× {l.quantidade}</span>{l.detidoMenosDe365Dias && <span className="etiqueta etiqueta-aviso">&lt; {t('365 dias', '365 days')}</span>}</td>
                        <td>{dataCurta(l.dataRealizacao)}</td><td className="num">{eur2.format(l.valorRealizacao)}</td>
                        <td>{dataCurta(l.dataAquisicao)}</td><td className="num">{eur2.format(l.valorAquisicao)}</td>
                        <td className="num">{eur2.format(l.despesas)}</td>
                        <td className={`num ${l.resultado >= 0 ? 'ganho' : 'perda'}`}>{eur2.format(l.resultado)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="cartao tabela-cartao">
            <h2>{t('Quadro 8A · Dividendos', 'Table 8A · Dividends')} <span className="suave pequeno">{t('(rendimentos de capitais obtidos no estrangeiro)', '(investment income earned abroad)')}</span></h2>
            <Explicacao titulo={t('Como ler esta tabela', 'How to read this table')}>
              <p>
                {t('Uma linha por país da fonte, com o código E11 (dividendos). O país é o do emitente do título, tirado do ISIN: um ETF domiciliado na Irlanda (ISIN começado por IE) é Irlanda, mesmo que invista em empresas americanas.',
                  'One line per source country, with code E11 (dividends). The country is the issuer\'s, taken from the ISIN: an ETF domiciled in Ireland (ISIN starting IE) is Ireland, even if it invests in US companies.')}
              </p>
              <p>
                {t('"Imposto pago no estrangeiro" é a parte que abate ao IRS, já limitada à taxa da convenção. Se lá fora te retiveram mais, a diferença aparece por baixo e não se declara como crédito.',
                  '"Tax paid abroad" is the part deducted from IRS, already capped at the treaty rate. If more was withheld abroad, the difference shows underneath and is not claimed as a credit.')}
              </p>
            </Explicacao>
            {r.dividendos.length === 0 ? <p className="suave">{t(`Sem dividendos em ${r.ano}.`, `No dividends in ${r.ano}.`)}</p> : (
              <div className="tabela-rolar">
                <table>
                  <thead>
                    <tr><th>{t('Código', 'Code')}</th><th>{t('País', 'Country')}</th><th className="num">{t('Rendimento bruto', 'Gross income')}</th><th className="num">{t('Imposto pago no estrangeiro', 'Tax paid abroad')}</th><th className="num">{t('Pagamentos', 'Payments')}</th></tr>
                  </thead>
                  <tbody>
                    {r.dividendos.map(d => (
                      <tr key={d.pais}>
                        <td>{d.codigo}</td><td>{d.codigoPais} <span className="suave pequeno">({d.pais})</span></td>
                        <td className="num">{eur2.format(d.bruto)}</td><td className="num">
                          {eur2.format(d.retencao)}
                          {d.retencaoNaoCreditada > 0 && <div className="suave pequeno">+ {eur2.format(d.retencaoNaoCreditada)} {t('acima da convenção (não conta)', 'above the treaty rate (does not count)')}</div>}
                        </td><td className="num">{d.pagamentos}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <p className="suave pequeno">
            {t('FIFO por título (CIRS, art. 43.º), câmbio de referência do dia de cada operação (Banco de Portugal). Códigos de país ISO numéricos: confirma na tabela da declaração. Ações com G01; ETF e fundos com G20 (define o tipo de cada título no separador Operações).', 'FIFO per holding (CIRS, art. 43), reference rate on each transaction date (Banco de Portugal). Numeric ISO country codes: check the table in the return. Shares with G01; ETFs and funds with G20 (set each holding\'s type in the Transactions tab).')}
          </p>
        </>
      )}
    </>
  )
}

function Operacoes({ operacoes, aoMudar }: { operacoes: Operacao[]; aoMudar: () => void }) {
  const [manual, setManual] = useState(false)

  // Um título por ISIN, com o tipo que define o código do Anexo J.
  const titulos = [...new Map(operacoes.map(o => [o.ativo, o])).values()].sort((a, b) => a.nome.localeCompare(b.nome))

  return (
    <>
      <ImportarOperacoes aoImportar={aoMudar} />

      {manual ? <FormOperacao aoGuardar={() => { setManual(false); aoMudar() }} aoCancelar={() => setManual(false)} /> : (
        <button className="botao-grande" onClick={() => setManual(true)}><Icone nome="mais" tamanho={20} /> {t('Adicionar operação à mão', 'Add a transaction by hand')}</button>
      )}

      {titulos.length > 0 && (
        <section className="cartao tabela-cartao">
          <h2>{t('Títulos', 'Holdings')} <span className="suave pequeno">{t('(ação → código G01; ETF ou fundo → G20)', '(share → code G01; ETF or fund → G20)')}</span></h2>
          <p className="suave pequeno">{t('O tipo foi sugerido pelo nome. Confirma cada um: um ETF declarado como ação fica com o código errado no Anexo J.', 'The type was guessed from the name. Check each one: an ETF declared as a share gets the wrong code in Annex J.')}</p>
          <Explicacao titulo={t('Porque é que alguns títulos pedem o ISIN?', 'Why do some holdings ask for an ISIN?')}>
            <p>
              {t('O ISIN é o código internacional de cada título (ex.: US0378331005 é a Apple). As duas primeiras letras dizem o país do emitente, que é o país da fonte no Anexo J. Algumas corretoras (Revolut, XTB, eToro) só dão o ticker, como "AAPL", que não diz o país.',
                'The ISIN is the international code for each security (e.g. US0378331005 is Apple). The first two letters give the issuer country, which is the source country in Annex J. Some brokers (Revolut, XTB, eToro) only give the ticker, like "AAPL", which does not say the country.')}
            </p>
            <p>{t('Encontras o ISIN na ficha do título na app da corretora ou pesquisando "<nome> ISIN". Ao indicá-lo, todas as operações desse título passam a usá-lo.', 'You can find the ISIN on the security page in your broker app, or by searching "<name> ISIN". Once you enter it, every transaction for that holding uses it.')}</p>
          </Explicacao>
          <Explicacao>
            <p>
              {t('No quadro 9.2A, cada venda leva um código conforme o que vendeste: G01 para ações e G20 para unidades de participação em fundos, que inclui os ETF. A taxa é a mesma (28%), mas um código errado é um erro na declaração.',
                'In table 9.2A, each sale gets a code for what you sold: G01 for shares and G20 for fund units, which includes ETFs. The rate is the same (28%), but a wrong code is an error on the return.')}
            </p>
            <p>{t('O portal adivinha pelo nome (palavras como "ETF", "UCITS", "iShares", "Acc"). Muda aqui o que estiver errado: a alteração vale para todas as operações desse título.', 'The portal guesses from the name (words like "ETF", "UCITS", "iShares", "Acc"). Change anything wrong here: it applies to every transaction for that holding.')}</p>
          </Explicacao>
          <div className="tabela-rolar">
            <table>
              <thead><tr><th>{t('Título', 'Holding')}</th><th>ISIN</th><th>{t('Tipo', 'Type')}</th><th>{t('Código', 'Code')}</th></tr></thead>
              <tbody>
                {titulos.map(titulo => (
                  <tr key={titulo.ativo}>
                    <td>{titulo.nome}</td>
                    <td><IsinTitulo ativo={titulo.ativo} aoMudar={aoMudar} /></td>
                    <td>
                      <select className="estado" value={titulo.tipoAtivo} aria-label={`${t('Tipo de', 'Type of')} ${titulo.nome}`}
                        onChange={async e => { await api.alterarTipoAtivo(titulo.ativo, e.target.value as TipoAtivo); aoMudar() }}>
                        <option value="Acao">{t('Ação', 'Share')}</option>
                        <option value="Etf">ETF</option>
                        <option value="Fundo">{t('Fundo', 'Fund')}</option>
                      </select>
                    </td>
                    <td><strong>{titulo.tipoAtivo === 'Acao' ? 'G01' : 'G20'}</strong></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {operacoes.length > 0 && (
        <section className="cartao tabela-cartao">
          <div className="pesquisa-topo">
            <h2>{operacoes.length} {t('operações', 'transactions')}</h2>
            <button className="ligacao" onClick={async () => {
              if (confirm(t('Apagar todas as operações? Podes voltar a importá-las.', 'Delete all transactions? You can import them again.'))) { await api.apagarTodasOperacoes(); aoMudar() }
            }}>{t('Apagar todas', 'Delete all')}</button>
          </div>
          <div className="tabela-rolar">
            <table>
              <thead>
                <tr><th>{t('Data', 'Date')}</th><th>{t('Tipo', 'Type')}</th><th>{t('Título', 'Holding')}</th><th className="num">{t('Qtd.', 'Qty')}</th><th className="num">{t('Preço', 'Price')}</th><th className="num">{t('Comissões', 'Fees')}</th><th className="num">{t('Retenção', 'Withheld')}</th><th /></tr>
              </thead>
              <tbody>
                {operacoes.map(o => (
                  <tr key={o.id}>
                    <td>{dataCurta(o.momento.slice(0, 10))}</td>
                    <td><span className={`etiqueta tipo-${o.tipo}`}>{tiposOperacao()[o.tipo]}</span></td>
                    <td><span title={o.ativo}>{o.nome}</span> <span className="suave pequeno">{o.ativo}</span></td>
                    <td className="num">{o.quantidade.toLocaleString('pt-PT')}</td>
                    <td className="num">{o.precoUnitario.toLocaleString('pt-PT')} {o.moeda}</td>
                    <td className="num">{o.comissoes ? `${o.comissoes.toLocaleString('pt-PT')} ${o.moedaComissoes ?? 'EUR'}` : '—'}</td>
                    <td className="num">{o.retencaoFonte ? `${o.retencaoFonte.toLocaleString('pt-PT')} ${o.moedaRetencao ?? o.moeda}` : '—'}</td>
                    <td><button className="icone-botao" aria-label={t('Apagar operação', 'Delete transaction')} onClick={async () => { await api.apagarOperacao(o.id); aoMudar() }}><Icone nome="lixo" tamanho={16} /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </>
  )
}

function FormOperacao({ aoGuardar, aoCancelar }: { aoGuardar: () => void; aoCancelar: () => void }) {
  const [o, setO] = useState<NovaOperacao>({
    tipo: 'Compra', momento: new Date().toISOString().slice(0, 10), ativo: '', nome: '', quantidade: 0, precoUnitario: 0,
    moeda: 'USD', comissoes: 0, retencaoFonte: 0, moedaRetencao: null, corretora: null, tipoAtivo: null,
  })
  const [erro, setErro] = useState<string | null>(null)
  const num = (v: string) => (v === '' ? 0 : Number(v))

  const submeter = async (e: FormEvent) => {
    e.preventDefault()
    try {
      await api.criarOperacao({ ...o, momento: o.momento + 'T00:00:00' })
      aoGuardar()
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  return (
    <form className="cartao formulario-linha" onSubmit={submeter}>
      <h2>{t('Nova operação', 'New transaction')}</h2>
      <div className="alternador" role="group" aria-label={t('Tipo', 'Type')}>
        {(['Compra', 'Venda', 'Dividendo'] as const).map(tipo => (
          <button key={tipo} type="button" aria-pressed={o.tipo === tipo} onClick={() => setO({ ...o, tipo })}>{tiposOperacao()[tipo]}</button>
        ))}
      </div>
      <div className="campos">
        <label>{t('Data', 'Date')}<input type="date" required value={o.momento} onChange={e => setO({ ...o, momento: e.target.value })} /></label>
        <label>ISIN<input required placeholder={t('ex.: US0378331005', 'e.g. US0378331005')} value={o.ativo} onChange={e => setO({ ...o, ativo: e.target.value })} /></label>
        <label>{t('Nome', 'Name')}<input placeholder="Apple" value={o.nome} onChange={e => setO({ ...o, nome: e.target.value })} /></label>
        <label>{o.tipo === 'Dividendo' ? t('N.º de títulos', 'Number of shares') : t('Quantidade', 'Quantity')}
          <input type="number" required min={0} step="any" value={o.quantidade || ''} onChange={e => setO({ ...o, quantidade: num(e.target.value) })} />
        </label>
        <label>{o.tipo === 'Dividendo' ? t('Dividendo bruto por título', 'Gross dividend per share') : t('Preço por título', 'Price per share')}
          <input type="number" required min={0} step="any" value={o.precoUnitario || ''} onChange={e => setO({ ...o, precoUnitario: num(e.target.value) })} />
        </label>
        <label>{t('Moeda', 'Currency')}
          <select value={o.moeda} onChange={e => setO({ ...o, moeda: e.target.value })}>
            {['USD', 'EUR', 'GBP', 'CHF', 'CAD', 'JPY', 'DKK', 'SEK', 'NOK', 'HKD', 'AUD'].map(m => <option key={m}>{m}</option>)}
          </select>
        </label>
        {o.tipo !== 'Dividendo' ? (
          <label>{t('Comissões (€)', 'Fees (€)')}<input type="number" min={0} step="any" value={o.comissoes || ''} onChange={e => setO({ ...o, comissoes: num(e.target.value) })} /></label>
        ) : (
          <label>{t('Imposto retido', 'Tax withheld')} ({o.moeda})<input type="number" min={0} step="any" value={o.retencaoFonte || ''} onChange={e => setO({ ...o, retencaoFonte: num(e.target.value), moedaRetencao: o.moeda })} /></label>
        )}
      </div>
      {erro && <p className="erro">{erro}</p>}
      <div className="acoes">
        <button type="submit">{t('Guardar', 'Save')}</button>
        <button type="button" className="ligacao" onClick={aoCancelar}>{t('Cancelar', 'Cancel')}</button>
      </div>
    </form>
  )
}

const formatoIsin = /^[A-Z]{2}[A-Z0-9]{9}[0-9]$/

/** Mostra o ISIN; se a corretora só deu o ticker, pede-o (dá o país da fonte no Anexo J). */
function IsinTitulo({ ativo, aoMudar }: { ativo: string; aoMudar: () => void }) {
  const [isin, setIsin] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  if (formatoIsin.test(ativo)) return <span className="suave">{ativo}</span>

  const guardar = async (e: FormEvent) => {
    e.preventDefault()
    try {
      await api.alterarIsin(ativo, isin.trim().toUpperCase())
      aoMudar()
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  return (
    <form className="linha" onSubmit={guardar}>
      <span className="etiqueta etiqueta-aviso" title={t('Sem ISIN, o país da fonte fica por identificar', 'Without an ISIN, the source country is unknown')}>{ativo}: {t('falta o ISIN', 'ISIN missing')}</span>
      <input aria-label={`${t('ISIN de', 'ISIN for')} ${ativo}`} placeholder={t('ISIN (12 caracteres)', 'ISIN (12 characters)')} maxLength={12} value={isin}
        onChange={e => setIsin(e.target.value.toUpperCase())} style={{ width: '13ch' }} />
      <button type="submit" disabled={!formatoIsin.test(isin.trim())}>{t('Guardar', 'Save')}</button>
      {erro && <span className="erro pequeno">{erro}</span>}
    </form>
  )
}
