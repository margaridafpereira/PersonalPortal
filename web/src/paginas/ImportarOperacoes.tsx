import { useState, type ChangeEvent } from 'react'
import { api, type AnaliseFicheiro, type FicheiroImportado, type Mapeamento, type TipoOperacao } from '../api'
import { Explicacao } from '../componentes/Explicacao'
import { Icone } from '../componentes/Icone'

type Campo = { chave: keyof Mapeamento; nome: string; obrigatorio?: boolean; ajuda?: string }

/** Igual a Importacao.MaxValoresTipo no servidor: uma coluna com tantos valores diferentes não é a do tipo. */
const maxValoresTipo = 50

const campos: Campo[] = [
  { chave: 'data', nome: 'Data', obrigatorio: true },
  { chave: 'hora', nome: 'Hora', ajuda: 'se vier numa coluna à parte' },
  { chave: 'tipo', nome: 'Tipo de operação', ajuda: 'compra, venda, dividendo…' },
  { chave: 'ativo', nome: 'ISIN', obrigatorio: true },
  { chave: 'nome', nome: 'Nome do título' },
  { chave: 'quantidade', nome: 'Quantidade', obrigatorio: true },
  { chave: 'preco', nome: 'Preço por título', obrigatorio: true, ajuda: 'no dividendo: valor bruto por título' },
  { chave: 'moeda', nome: 'Moeda' },
  { chave: 'comissoes', nome: 'Comissões (€)', ajuda: 'em euros' },
  { chave: 'retencao', nome: 'Imposto retido' },
  { chave: 'moedaRetencao', nome: 'Moeda do imposto retido', ajuda: 'se for diferente da moeda do preço' },
  { chave: 'idExterno', nome: 'Id da operação', ajuda: 'evita duplicados ao reimportar' },
]

const nomesFormato: Record<Exclude<AnaliseFicheiro['formato'], 'universal'>, string> = {
  trading212: 'Trading 212',
  modelo: 'modelo do portal',
  degiro: 'Degiro (extrato de conta)',
  revolut: 'Revolut',
  xtb: 'XTB (operações de caixa)',
  etoro: 'eToro (atividade da conta)',
  ibkr: 'Interactive Brokers (negócios)',
  'ibkr-dividendos': 'Interactive Brokers (dividendos)',
}

/** Um Excel não se pode ler como texto: vai em base64 e o servidor converte-o. */
async function lerFicheiro(ficheiro: File): Promise<FicheiroImportado> {
  if (!/\.xlsx$/i.test(ficheiro.name))
    return { conteudo: await ficheiro.text() }
  const bytes = new Uint8Array(await ficheiro.arrayBuffer())
  let binario = ''
  for (let i = 0; i < bytes.length; i += 0x8000)
    binario += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
  return { xlsx: btoa(binario) }
}

/**
 * Importação de qualquer corretora. A pessoa só escolhe o ficheiro (CSV ou Excel): o servidor reconhece os formatos
 * que conhece e, nos outros, sugere as colunas para a pessoa confirmar.
 */
export function ImportarOperacoes({ aoImportar }: { aoImportar: () => void }) {
  const [conteudo, setConteudo] = useState<FicheiroImportado | null>(null)
  const [analise, setAnalise] = useState<AnaliseFicheiro | null>(null)
  const [mapa, setMapa] = useState<Mapeamento | null>(null)
  const [resultado, setResultado] = useState<{ importadas: number; avisos: string[]; reconhecido?: string } | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const escolher = async (e: ChangeEvent<HTMLInputElement>) => {
    const ficheiro = e.target.files?.[0]
    e.target.value = ''
    if (!ficheiro) return
    setErro(null)
    setResultado(null)
    try {
      if (/\.(xls|pdf)$/i.test(ficheiro.name))
        throw new Error(ficheiro.name.toLowerCase().endsWith('.pdf')
          ? 'Os PDF ainda não são suportados: exporta o histórico em CSV ou Excel (vê "Onde encontro o ficheiro?").'
          : 'Este é o formato Excel antigo (.xls). Abre-o no Excel e guarda como .xlsx, ou exporta em CSV.')
      const dados = await lerFicheiro(ficheiro)
      const a = await api.analisar(dados)
      if (a.formato !== 'universal') {
        setAnalise(null)
        setResultado({ ...(await api.importar(a.formato, dados)), reconhecido: nomesFormato[a.formato] })
        aoImportar()
        return
      }
      setConteudo(dados)
      setAnalise(a)
      setMapa(a.sugestao ?? {
        data: -1, hora: null, tipo: null, ativo: -1, nome: null, quantidade: -1, preco: -1, moeda: null, moedaFixa: 'EUR',
        comissoes: null, retencao: null, moedaRetencao: null, idExterno: null, valoresTipo: {}, vendaSeQuantidadeNegativa: true,
      })
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  const importar = async () => {
    if (!conteudo || !mapa) return
    try {
      setResultado(await api.importar('universal', conteudo, mapa))
      setAnalise(null)
      setConteudo(null)
      aoImportar()
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  const mudarColuna = (c: Campo, coluna: number | null) => {
    if (!mapa || !analise) return
    // Mudar a coluna do tipo troca a lista de valores pela da coluna nova, com o tipo sugerido pelo servidor.
    // A lista é exatamente o que o servidor usa: um valor fora dela é ignorado, por isso o ecrã nunca diz
    // "Ignorar" para algo que depois seria importado.
    if (c.chave === 'tipo')
      setMapa({ ...mapa, tipo: coluna, valoresTipo: coluna === null ? {} : { ...analise.valoresPorColuna[coluna] } })
    else
      setMapa({ ...mapa, [c.chave]: coluna })
  }

  const obrigatoriosOk = mapa !== null && campos.filter(c => c.obrigatorio).every(c => (mapa[c.chave] as number) >= 0)
    && (mapa.moeda !== null || /^[A-Za-z]{3}$/.test(mapa.moedaFixa ?? ''))
  const colunas = analise?.colunas.map((c, i) => c.trim() || `(coluna ${i + 1} sem nome)`) ?? []
  const valoresTipo = mapa?.tipo != null ? Object.keys(mapa.valoresTipo) : []

  return (
    <div className="cartao formulario-linha">
      <h2><Icone nome="mais" tamanho={18} /> Importar operações</h2>
      <p className="suave pequeno">
        Exporta o histórico da tua corretora em CSV ou Excel e escolhe o ficheiro. O portal reconhece Trading 212, Degiro, Revolut, XTB, eToro e Interactive Brokers;
        de outra corretora, sugere o que é cada coluna e tu confirmas.
      </p>
      <label className="botao escolher-ficheiro">
        <Icone nome="mais" tamanho={18} /> Escolher ficheiro (CSV ou Excel)
        <input type="file" accept=".csv,.txt,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={escolher} hidden />
      </label>
      <Explicacao titulo="Porque é preciso o histórico completo?">
        <p>
          A lei manda calcular as mais-valias pelo método FIFO: quando vendes, considera-se que vendeste primeiro os títulos que compraste há mais tempo.
          Para saber quanto pagaste por eles, o portal precisa das compras antigas, mesmo as de anos já declarados.
          Sem elas, a venda aparece com um aviso de "faltam compras".
        </p>
        <p>Podes importar o mesmo ficheiro outra vez, ou ficheiros que se sobrepõem: as operações repetidas são reconhecidas e ignoradas.</p>
      </Explicacao>
      <Explicacao titulo="Onde encontro o ficheiro?">
        <p>Escolhe sempre o período desde a abertura da conta. Os menus mudam de vez em quando; se não encontrares, procura "Exportar" ou "Extrato".</p>
        <p><strong>Trading 212</strong>: Histórico → ícone de exportar → período → CSV.</p>
        <p><strong>Degiro</strong>: Caixa de entrada → Extrato de conta → período → CSV. É este ficheiro (e não o de "Transações") que traz também os dividendos e os custos.</p>
        <p><strong>Revolut</strong>: Investir → Mais → Documentos → Extrato de conta → Excel ou CSV.</p>
        <p><strong>XTB</strong>: Histórico da conta → Operações de caixa → Exportar. O ficheiro não traz a moeda da conta: o portal assume euros.</p>
        <p><strong>eToro</strong>: Portefólio → Histórico → ícone do extrato de conta → Excel. O portal lê a folha "Account Activity".</p>
        <p>
          <strong>Interactive Brokers</strong>: Relatórios → Flex Queries → criar uma consulta em CSV com a secção "Trades"
          (campos Buy/Sell, TradeDate, ISIN, Description, Quantity, TradePrice, CurrencyPrimary, IBCommission, IBCommissionCurrency, TradeID)
          e outra com "Cash Transactions" (Type, SettleDate, ISIN, Description, Amount, CurrencyPrimary) para os dividendos. Importa os dois ficheiros.
        </p>
        <p>
          <strong>Outras corretoras</strong> (Trade Republic, Lightyear…): qualquer CSV ou Excel com as operações serve; confirmas as colunas.
          Se a corretora só dá PDF, preenche o <a href="/api/investimentos/modelo.csv" download>modelo em CSV</a> (uma linha por compra, venda ou dividendo).
        </p>
      </Explicacao>
      <Explicacao titulo="Posso confiar na leitura automática?">
        <p>
          Os leitores de cada corretora foram feitos a partir de ficheiros de exemplo públicos e de conversores open source, não do teu ficheiro.
          Por isso, depois de importar, compara duas ou três operações com o extrato da corretora (quantidade, preço, data).
          Se algo não bater certo, apaga as operações importadas e usa a leitura por colunas, ou avisa para o leitor ser corrigido.
        </p>
      </Explicacao>

      {analise && mapa && (
        <div className="mapeamento">
          <h3>Confirma as colunas</h3>
          <p className="suave pequeno">
            Não conheço o formato deste ficheiro, por isso tentei adivinhar o que é cada coluna pelo nome.
            Confirma com as primeiras linhas, mais abaixo: um erro aqui (por exemplo, o preço total em vez do preço por título) muda o imposto.
          </p>
          {analise.camposEmFalta.length > 0 && (
            <p className="aviso pequeno"><Icone nome="alerta" tamanho={14} /> Não reconheci: {analise.camposEmFalta.join(', ')}. Escolhe-as abaixo.</p>
          )}
          <div className="campos">
            {campos.map(c => (
              <label key={c.chave}>
                {c.nome}{c.obrigatorio && ' *'}
                <select value={(mapa[c.chave] as number | null) ?? ''}
                  onChange={e => mudarColuna(c, e.target.value === '' ? (c.obrigatorio ? -1 : null) : Number(e.target.value))}>
                  <option value="">{c.obrigatorio ? '— escolher —' : '— não existe —'}</option>
                  {colunas.map((nome, i) => <option key={i} value={i}>{nome}</option>)}
                </select>
                {c.ajuda && <span className="suave pequeno">{c.ajuda}</span>}
              </label>
            ))}
            {mapa.moeda === null && (
              <label>Moeda de todas as operações
                <input maxLength={3} value={mapa.moedaFixa ?? ''} onChange={e => setMapa({ ...mapa, moedaFixa: e.target.value.toUpperCase() })} />
              </label>
            )}
          </div>

          {mapa.tipo === null ? (
            <label className="linha">
              <input type="checkbox" checked={mapa.vendaSeQuantidadeNegativa} onChange={e => setMapa({ ...mapa, vendaSeQuantidadeNegativa: e.target.checked })} />
              Quantidade negativa = venda (ex.: Degiro)
            </label>
          ) : valoresTipo.length > 0 && (
            <>
              <p className="pequeno"><strong>O que significa cada tipo?</strong></p>
              <p className="suave pequeno">
                Só entram as linhas com Compra, Venda ou Dividendo. Depósitos, levantamentos, juros e câmbios não contam para o Anexo J e ficam em "Ignorar".
              </p>
              {valoresTipo.length >= maxValoresTipo && (
                <p className="aviso pequeno"><Icone nome="alerta" tamanho={14} /> Esta coluna tem muitos valores diferentes: confirma que é a do tipo de operação.</p>
              )}
              <div className="campos">
                {valoresTipo.map(v => (
                  <label key={v}>“{v}”
                    <select value={mapa.valoresTipo[v] ?? ''}
                      onChange={e => setMapa({ ...mapa, valoresTipo: { ...mapa.valoresTipo, [v]: (e.target.value || null) as TipoOperacao | null } })}>
                      <option value="">Ignorar</option>
                      <option value="Compra">Compra</option>
                      <option value="Venda">Venda</option>
                      <option value="Dividendo">Dividendo</option>
                    </select>
                  </label>
                ))}
              </div>
            </>
          )}

          <p className="pequeno"><strong>Primeiras linhas do ficheiro</strong></p>
          <div className="tabela-rolar">
            <table>
              <thead><tr>{colunas.map((c, i) => <th key={i}>{c}</th>)}</tr></thead>
              <tbody>{analise.amostra.map((l, i) => <tr key={i}>{l.map((v, j) => <td key={j}>{v}</td>)}</tr>)}</tbody>
            </table>
          </div>

          <div className="acoes">
            <button onClick={importar} disabled={!obrigatoriosOk}>Importar</button>
            <button className="ligacao" onClick={() => setAnalise(null)}>Cancelar</button>
          </div>
        </div>
      )}

      {erro && <p className="erro">{erro}</p>}
      {resultado && (
        <div className={resultado.importadas > 0 ? 'estimativa' : 'aviso pequeno'}>
          {resultado.reconhecido && <>Ficheiro reconhecido: {resultado.reconhecido}. </>}
          {resultado.importadas} operação(ões) importadas.
          {resultado.avisos.length > 0 && <ul className="pequeno">{resultado.avisos.map(a => <li key={a}>{a}</li>)}</ul>}
        </div>
      )}
    </div>
  )
}
