import { useEffect, useState, type FormEvent } from 'react'
import { api, dataCurta, euros, type NovaOperacao, type Operacao, type RelatorioIrs, type TipoAtivo } from '../api'
import { Explicacao } from '../componentes/Explicacao'
import { Icone } from '../componentes/Icone'
import { ImportarOperacoes } from './ImportarOperacoes'

const eur2 = new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' })
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
          <h1>IRS de investimentos</h1>
          <p className="suave">Mais-valias e dividendos de corretoras estrangeiras, convertidos ao câmbio do Banco de Portugal e prontos para o Anexo J.</p>
        </div>
      </header>

      <p className="aviso pequeno">
        <Icone nome="alerta" tamanho={14} />
        Estimativa indicativa. Confirma os valores antes de entregar a declaração; em caso de dúvida, fala com um contabilista.
      </p>

      <div className="separadores" role="tablist">
        <button role="tab" aria-selected={separador === 'relatorio'} onClick={() => setSeparador('relatorio')}>
          <Icone nome="escudo" tamanho={18} /> Anexo J
        </button>
        <button role="tab" aria-selected={separador === 'operacoes'} onClick={() => setSeparador('operacoes')}>
          <Icone nome="calendario" tamanho={18} /> Operações <span className="contador">{operacoes?.length ?? 0}</span>
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
        <p>Ainda não tens operações.</p>
        <button onClick={aoImportar}>Importar da corretora</button>
      </div>
    )

  return (
    <>
      <div className="barra-acoes">
        <label className="linha">Rendimentos de
          <select value={ano} onChange={e => setAno(Number(e.target.value))} style={{ width: 'auto' }}>
            {anos.map(a => <option key={a}>{a}</option>)}
          </select>
        </label>
        <span className="suave pequeno">Declaração entregue entre abril e junho de {ano + 1}</span>
      </div>

      {r && r.maisValias.length === 0 && r.dividendos.length === 0 && (
        <div className="aviso pequeno">
          <Icone nome="alerta" tamanho={14} />
          <div>
            <p>Não há nada a declarar em {ano}: o Anexo J de um ano só leva as <strong>vendas</strong> e os <strong>dividendos</strong> desse ano (as compras só servem para calcular o custo quando vendes).</p>
            {comRendimentos.length > 0 && (
              <p className="linha">
                Anos com rendimentos:
                {comRendimentos.map(a => (
                  <button key={a.ano} className="ligacao pequeno" onClick={() => setAno(a.ano)}>{a.ano} ({a.tipos.join(' e ')})</button>
                ))}
              </p>
            )}
          </div>
        </div>
      )}

      {erro?.ano === ano && !r ? <p className="erro">Não foi possível calcular o relatório: {erro.mensagem}</p>
        : !r ? <p>A calcular (a obter os câmbios do Banco de Portugal)…</p> : (
        <>
          <div className="indicadores resumo-irs">
            <div className={`indicador ${r.saldoMaisValias >= 0 ? 'tom-positivo' : 'tom-aviso'}`}>
              <strong>{euros(r.saldoMaisValias)}</strong><span>saldo de mais-valias</span>
              <Explicacao titulo="O que é?">
                <p>Soma, em euros, de todas as vendas do ano: valor de venda − valor de compra − despesas. A perda num título compensa o ganho noutro.</p>
                <p>
                  Se o saldo for positivo, paga 28% (taxa autónoma). Em alternativa podes optar pelo englobamento: somar este saldo aos outros rendimentos
                  e pagar à taxa do teu escalão, o que compensa se essa taxa for inferior a 28%. O portal não simula o englobamento.
                </p>
                <p>Um saldo negativo não paga nada; só pode ser reportado aos anos seguintes se optares pelo englobamento.</p>
              </Explicacao>
            </div>
            <div className="indicador">
              <strong>{euros(r.dividendosBrutos)}</strong><span>dividendos brutos</span>
              <Explicacao titulo="O que é?">
                <p>
                  O valor dos dividendos antes de qualquer imposto, convertido para euros ao câmbio do dia do pagamento.
                  Declara-se o bruto, não o que chegou à conta: o imposto retido lá fora entra à parte, como crédito.
                </p>
              </Explicacao>
            </div>
            <div className="indicador">
              <strong>{euros(r.retencaoEstrangeiro)}</strong><span>retido no estrangeiro que conta</span>
              <Explicacao>
                <p>
                  Quando recebes dividendos de uma empresa estrangeira, o país dela fica logo com uma parte (retenção na fonte).
                  Para não pagares duas vezes, esse valor abate ao imposto português: é o crédito por dupla tributação.
                </p>
                <p>
                  Mas só abate até à taxa da convenção entre Portugal e esse país. Nos EUA é 15%: se te retiveram 30%
                  (acontece quando a corretora não tem o teu formulário W-8BEN), os outros 15% não abatem ao IRS.
                  Esse excesso só se recupera pedindo-o às finanças dos EUA; para o evitar, confirma na corretora que o W-8BEN está preenchido.
                </p>
                {r.dividendos.some(d => d.retencaoNaoCreditada > 0) && (
                  <p><strong>Este ano, {euros(r.dividendos.reduce((s, d) => s + d.retencaoNaoCreditada, 0))} foram retidos acima da convenção e não contam.</strong></p>
                )}
              </Explicacao>
            </div>
            <div className="indicador tom-aviso">
              <strong>{euros(r.impostoMaisValias + r.impostoDividendos)}</strong><span>imposto estimado (28%)</span>
              <Explicacao titulo="Como é calculado?">
                <p>
                  28% do saldo positivo de mais-valias ({euros(r.impostoMaisValias)}) + 28% dos dividendos brutos menos o imposto
                  já pago lá fora, país a país ({euros(r.impostoDividendos)}).
                </p>
                <p>É uma estimativa: a conta final é feita pelas Finanças, com todos os teus rendimentos e as opções que escolheres na declaração.</p>
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
            <h2>Quadro 9.2A · Mais-valias <span className="suave pequeno">(alienação onerosa de partes sociais e outros valores mobiliários)</span></h2>
            <Explicacao titulo="Como ler esta tabela">
              <p>
                Cada linha é uma venda casada com a compra de onde vieram os títulos. Pelo método FIFO, vendem-se primeiro os títulos comprados há mais tempo;
                se uma venda levar títulos de duas compras, aparece em duas linhas. É assim que se preenche o quadro.
              </p>
              <p>
                Os valores estão em euros, ao câmbio de referência do Banco de Portugal no dia de cada operação (o da compra para a aquisição, o da venda para a realização).
                As despesas são as comissões da compra e da venda, na proporção dos títulos vendidos.
              </p>
              <p>
                <strong>País</strong>: código numérico do país do emitente, tirado das duas primeiras letras do ISIN; confirma na tabela da declaração.
                {' '}<strong>Código</strong>: G01 para ações, G20 para ETF e fundos.
                {' '}<strong>&lt; 365 dias</strong>: títulos detidos menos de um ano. Se o teu rendimento coletável total chegar ao último escalão do IRS, o ganho destes títulos tem de ser englobado.
              </p>
            </Explicacao>
            {r.maisValias.length === 0 ? <p className="suave">Sem vendas em {r.ano}.</p> : (
              <div className="tabela-rolar">
                <table>
                  <thead>
                    <tr>
                      <th>País</th><th>Código</th><th>Título</th>
                      <th>Realização</th><th className="num">Valor</th>
                      <th>Aquisição</th><th className="num">Valor</th>
                      <th className="num">Despesas</th><th className="num">Resultado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {r.maisValias.map((l, i) => (
                      <tr key={i}>
                        <td title={l.pais}>{l.codigoPais}</td>
                        <td>{l.codigo}</td>
                        <td><span title={l.ativo}>{l.nome}</span> <span className="suave pequeno">× {l.quantidade}</span>{l.detidoMenosDe365Dias && <span className="etiqueta etiqueta-aviso">&lt; 365 dias</span>}</td>
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
            <h2>Quadro 8A · Dividendos <span className="suave pequeno">(rendimentos de capitais obtidos no estrangeiro)</span></h2>
            <Explicacao titulo="Como ler esta tabela">
              <p>
                Uma linha por país da fonte, com o código E11 (dividendos). O país é o do emitente do título, tirado do ISIN: um ETF domiciliado na Irlanda
                (ISIN começado por IE) é Irlanda, mesmo que invista em empresas americanas.
              </p>
              <p>
                "Imposto pago no estrangeiro" é a parte que abate ao IRS, já limitada à taxa da convenção. Se lá fora te retiveram mais, a diferença aparece por baixo e não se declara como crédito.
              </p>
            </Explicacao>
            {r.dividendos.length === 0 ? <p className="suave">Sem dividendos em {r.ano}.</p> : (
              <div className="tabela-rolar">
                <table>
                  <thead>
                    <tr><th>Código</th><th>País</th><th className="num">Rendimento bruto</th><th className="num">Imposto pago no estrangeiro</th><th className="num">Pagamentos</th></tr>
                  </thead>
                  <tbody>
                    {r.dividendos.map(d => (
                      <tr key={d.pais}>
                        <td>{d.codigo}</td><td>{d.codigoPais} <span className="suave pequeno">({d.pais})</span></td>
                        <td className="num">{eur2.format(d.bruto)}</td><td className="num">
                          {eur2.format(d.retencao)}
                          {d.retencaoNaoCreditada > 0 && <div className="suave pequeno">+ {eur2.format(d.retencaoNaoCreditada)} acima da convenção (não conta)</div>}
                        </td><td className="num">{d.pagamentos}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <p className="suave pequeno">
            FIFO por título (CIRS, art. 43.º), câmbio de referência do dia de cada operação (Banco de Portugal).
            Códigos de país ISO numéricos: confirma na tabela da declaração. Ações com G01; ETF e fundos com G20 (define o tipo de cada título no separador Operações).
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
        <button className="botao-grande" onClick={() => setManual(true)}><Icone nome="mais" tamanho={20} /> Adicionar operação à mão</button>
      )}

      {titulos.length > 0 && (
        <section className="cartao tabela-cartao">
          <h2>Títulos <span className="suave pequeno">(ação → código G01; ETF ou fundo → G20)</span></h2>
          <p className="suave pequeno">O tipo foi sugerido pelo nome. Confirma cada um: um ETF declarado como ação fica com o código errado no Anexo J.</p>
          <Explicacao titulo="Porque é que alguns títulos pedem o ISIN?">
            <p>
              O ISIN é o código internacional de cada título (ex.: US0378331005 é a Apple). As duas primeiras letras dizem o país do emitente,
              que é o país da fonte no Anexo J. Algumas corretoras (Revolut, XTB, eToro) só dão o ticker, como "AAPL", que não diz o país.
            </p>
            <p>Encontras o ISIN na ficha do título na app da corretora ou pesquisando "&lt;nome&gt; ISIN". Ao indicá-lo, todas as operações desse título passam a usá-lo.</p>
          </Explicacao>
          <Explicacao>
            <p>
              No quadro 9.2A, cada venda leva um código conforme o que vendeste: G01 para ações e G20 para unidades de participação em fundos, que inclui os ETF.
              A taxa é a mesma (28%), mas um código errado é um erro na declaração.
            </p>
            <p>O portal adivinha pelo nome (palavras como "ETF", "UCITS", "iShares", "Acc"). Muda aqui o que estiver errado: a alteração vale para todas as operações desse título.</p>
          </Explicacao>
          <div className="tabela-rolar">
            <table>
              <thead><tr><th>Título</th><th>ISIN</th><th>Tipo</th><th>Código</th></tr></thead>
              <tbody>
                {titulos.map(t => (
                  <tr key={t.ativo}>
                    <td>{t.nome}</td>
                    <td><IsinTitulo ativo={t.ativo} aoMudar={aoMudar} /></td>
                    <td>
                      <select className="estado" value={t.tipoAtivo} aria-label={`Tipo de ${t.nome}`}
                        onChange={async e => { await api.alterarTipoAtivo(t.ativo, e.target.value as TipoAtivo); aoMudar() }}>
                        <option value="Acao">Ação</option>
                        <option value="Etf">ETF</option>
                        <option value="Fundo">Fundo</option>
                      </select>
                    </td>
                    <td><strong>{t.tipoAtivo === 'Acao' ? 'G01' : 'G20'}</strong></td>
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
            <h2>{operacoes.length} operações</h2>
            <button className="ligacao" onClick={async () => {
              if (confirm('Apagar todas as operações? Podes voltar a importá-las.')) { await api.apagarTodasOperacoes(); aoMudar() }
            }}>Apagar todas</button>
          </div>
          <div className="tabela-rolar">
            <table>
              <thead>
                <tr><th>Data</th><th>Tipo</th><th>Título</th><th className="num">Qtd.</th><th className="num">Preço</th><th className="num">Comissões</th><th className="num">Retenção</th><th /></tr>
              </thead>
              <tbody>
                {operacoes.map(o => (
                  <tr key={o.id}>
                    <td>{dataCurta(o.momento.slice(0, 10))}</td>
                    <td><span className={`etiqueta tipo-${o.tipo}`}>{o.tipo}</span></td>
                    <td><span title={o.ativo}>{o.nome}</span> <span className="suave pequeno">{o.ativo}</span></td>
                    <td className="num">{o.quantidade.toLocaleString('pt-PT')}</td>
                    <td className="num">{o.precoUnitario.toLocaleString('pt-PT')} {o.moeda}</td>
                    <td className="num">{o.comissoes ? `${o.comissoes.toLocaleString('pt-PT')} ${o.moedaComissoes ?? 'EUR'}` : '—'}</td>
                    <td className="num">{o.retencaoFonte ? `${o.retencaoFonte.toLocaleString('pt-PT')} ${o.moedaRetencao ?? o.moeda}` : '—'}</td>
                    <td><button className="icone-botao" aria-label="Apagar operação" onClick={async () => { await api.apagarOperacao(o.id); aoMudar() }}><Icone nome="lixo" tamanho={16} /></button></td>
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
      <h2>Nova operação</h2>
      <div className="alternador" role="group" aria-label="Tipo">
        {(['Compra', 'Venda', 'Dividendo'] as const).map(t => (
          <button key={t} type="button" aria-pressed={o.tipo === t} onClick={() => setO({ ...o, tipo: t })}>{t}</button>
        ))}
      </div>
      <div className="campos">
        <label>Data<input type="date" required value={o.momento} onChange={e => setO({ ...o, momento: e.target.value })} /></label>
        <label>ISIN<input required placeholder="ex.: US0378331005" value={o.ativo} onChange={e => setO({ ...o, ativo: e.target.value })} /></label>
        <label>Nome<input placeholder="Apple" value={o.nome} onChange={e => setO({ ...o, nome: e.target.value })} /></label>
        <label>{o.tipo === 'Dividendo' ? 'N.º de títulos' : 'Quantidade'}
          <input type="number" required min={0} step="any" value={o.quantidade || ''} onChange={e => setO({ ...o, quantidade: num(e.target.value) })} />
        </label>
        <label>{o.tipo === 'Dividendo' ? 'Dividendo bruto por título' : 'Preço por título'}
          <input type="number" required min={0} step="any" value={o.precoUnitario || ''} onChange={e => setO({ ...o, precoUnitario: num(e.target.value) })} />
        </label>
        <label>Moeda
          <select value={o.moeda} onChange={e => setO({ ...o, moeda: e.target.value })}>
            {['USD', 'EUR', 'GBP', 'CHF', 'CAD', 'JPY', 'DKK', 'SEK', 'NOK', 'HKD', 'AUD'].map(m => <option key={m}>{m}</option>)}
          </select>
        </label>
        {o.tipo !== 'Dividendo' ? (
          <label>Comissões (€)<input type="number" min={0} step="any" value={o.comissoes || ''} onChange={e => setO({ ...o, comissoes: num(e.target.value) })} /></label>
        ) : (
          <label>Imposto retido ({o.moeda})<input type="number" min={0} step="any" value={o.retencaoFonte || ''} onChange={e => setO({ ...o, retencaoFonte: num(e.target.value), moedaRetencao: o.moeda })} /></label>
        )}
      </div>
      {erro && <p className="erro">{erro}</p>}
      <div className="acoes">
        <button type="submit">Guardar</button>
        <button type="button" className="ligacao" onClick={aoCancelar}>Cancelar</button>
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
      <span className="etiqueta etiqueta-aviso" title="Sem ISIN, o país da fonte fica por identificar">{ativo}: falta o ISIN</span>
      <input aria-label={`ISIN de ${ativo}`} placeholder="ISIN (12 caracteres)" maxLength={12} value={isin}
        onChange={e => setIsin(e.target.value.toUpperCase())} style={{ width: '13ch' }} />
      <button type="submit" disabled={!formatoIsin.test(isin.trim())}>Guardar</button>
      {erro && <span className="erro pequeno">{erro}</span>}
    </form>
  )
}
