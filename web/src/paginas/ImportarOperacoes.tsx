import { useState, type ChangeEvent } from 'react'
import { api, type AnaliseFicheiro, type FicheiroImportado, type Mapeamento, type TipoOperacao } from '../api'
import { Explicacao } from '../componentes/Explicacao'
import { Icone } from '../componentes/Icone'
import { t } from '../i18n'

type Campo = { chave: keyof Mapeamento; nome: string; obrigatorio?: boolean; ajuda?: string }

/** Igual a Importacao.MaxValoresTipo no servidor: uma coluna com tantos valores diferentes não é a do tipo. */
const maxValoresTipo = 50

const campos = (): Campo[] => [
  { chave: 'data', nome: t('Data', 'Date'), obrigatorio: true },
  { chave: 'hora', nome: t('Hora', 'Time'), ajuda: t('se vier numa coluna à parte', 'if it is in a separate column') },
  { chave: 'tipo', nome: t('Tipo de operação', 'Transaction type'), ajuda: t('compra, venda, dividendo…', 'buy, sell, dividend…') },
  { chave: 'ativo', nome: 'ISIN', obrigatorio: true },
  { chave: 'nome', nome: t('Nome do título', 'Holding name') },
  { chave: 'quantidade', nome: t('Quantidade', 'Quantity'), obrigatorio: true },
  { chave: 'preco', nome: t('Preço por título', 'Price per share'), obrigatorio: true, ajuda: t('no dividendo: valor bruto por título', 'for dividends: gross amount per share') },
  { chave: 'moeda', nome: t('Moeda', 'Currency') },
  { chave: 'comissoes', nome: t('Comissões (€)', 'Fees (€)'), ajuda: t('em euros', 'in euros') },
  { chave: 'retencao', nome: t('Imposto retido', 'Tax withheld') },
  { chave: 'moedaRetencao', nome: t('Moeda do imposto retido', 'Withholding tax currency'), ajuda: t('se for diferente da moeda do preço', 'if different from the price currency') },
  { chave: 'idExterno', nome: t('Id da operação', 'Transaction id'), ajuda: t('evita duplicados ao reimportar', 'avoids duplicates when re-importing') },
]

const nomesFormato = (): Record<Exclude<AnaliseFicheiro['formato'], 'universal'>, string> => ({
  trading212: 'Trading 212',
  modelo: t('modelo do portal', 'portal template'),
  degiro: t('Degiro (extrato de conta)', 'Degiro (account statement)'),
  revolut: 'Revolut',
  xtb: t('XTB (operações de caixa)', 'XTB (cash operations)'),
  etoro: t('eToro (atividade da conta)', 'eToro (account activity)'),
  ibkr: t('Interactive Brokers (negócios)', 'Interactive Brokers (trades)'),
  'ibkr-dividendos': t('Interactive Brokers (dividendos)', 'Interactive Brokers (dividends)'),
})

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
          ? t('Os PDF ainda não são suportados: exporta o histórico em CSV ou Excel (vê "Onde encontro o ficheiro?").', 'PDFs are not supported yet: export the history as CSV or Excel (see "Where do I find the file?").')
          : t('Este é o formato Excel antigo (.xls). Abre-o no Excel e guarda como .xlsx, ou exporta em CSV.', 'This is the old Excel format (.xls). Open it in Excel and save as .xlsx, or export to CSV.'))
      const dados = await lerFicheiro(ficheiro)
      const a = await api.analisar(dados)
      if (a.formato !== 'universal') {
        setAnalise(null)
        setResultado({ ...(await api.importar(a.formato, dados)), reconhecido: nomesFormato()[a.formato] })
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

  const obrigatoriosOk = mapa !== null && campos().filter(c => c.obrigatorio).every(c => (mapa[c.chave] as number) >= 0)
    && (mapa.moeda !== null || /^[A-Za-z]{3}$/.test(mapa.moedaFixa ?? ''))
  const colunas = analise?.colunas.map((c, i) => c.trim() || t(`(coluna ${i + 1} sem nome)`, `(column ${i + 1}, no name)`)) ?? []
  const valoresTipo = mapa?.tipo != null ? Object.keys(mapa.valoresTipo) : []

  return (
    <div className="cartao formulario-linha">
      <h2><Icone nome="mais" tamanho={18} /> {t('Importar operações', 'Import transactions')}</h2>
      <p className="suave pequeno">
        {t('Exporta o histórico da tua corretora em CSV ou Excel e escolhe o ficheiro. O portal reconhece Trading 212, Degiro, Revolut, XTB, eToro e Interactive Brokers; de outra corretora, sugere o que é cada coluna e tu confirmas.', 'Export your broker history as CSV or Excel and choose the file. The portal recognises Trading 212, Degiro, Revolut, XTB, eToro and Interactive Brokers; for any other broker, it suggests what each column is and you confirm.')}
      </p>
      <label className="botao escolher-ficheiro">
        <Icone nome="mais" tamanho={18} /> {t('Escolher ficheiro (CSV ou Excel)', 'Choose a file (CSV or Excel)')}
        <input type="file" accept=".csv,.txt,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={escolher} hidden />
      </label>
      <Explicacao titulo={t('Porque é preciso o histórico completo?', 'Why is the full history needed?')}>
        <p>
          {t('A lei manda calcular as mais-valias pelo método FIFO: quando vendes, considera-se que vendeste primeiro os títulos que compraste há mais tempo. Para saber quanto pagaste por eles, o portal precisa das compras antigas, mesmo as de anos já declarados. Sem elas, a venda aparece com um aviso de "faltam compras".',
            'The law requires capital gains to be worked out with FIFO: when you sell, the oldest shares are treated as sold first. To know what you paid for them, the portal needs the old purchases, even from years already declared. Without them, the sale shows a "missing purchases" warning.')}
        </p>
        <p>{t('Podes importar o mesmo ficheiro outra vez, ou ficheiros que se sobrepõem: as operações repetidas são reconhecidas e ignoradas.', 'You can import the same file again, or overlapping files: repeated transactions are recognised and skipped.')}</p>
      </Explicacao>
      <Explicacao titulo={t('Onde encontro o ficheiro?', 'Where do I find the file?')}>
        <p>{t('Escolhe sempre o período desde a abertura da conta. Os menus mudam de vez em quando; se não encontrares, procura "Exportar" ou "Extrato".', 'Always choose the period since the account was opened. Menus change from time to time; if you cannot find it, look for "Export" or "Statement".')}</p>
        <p><strong>Trading 212</strong>: {t('Histórico → ícone de exportar → período → CSV.', 'History → export icon → period → CSV.')}</p>
        <p><strong>Degiro</strong>: {t('Caixa de entrada → Extrato de conta → período → CSV. É este ficheiro (e não o de "Transações") que traz também os dividendos e os custos.', 'Inbox → Account statement → period → CSV. This file (not "Transactions") also has the dividends and costs.')}</p>
        <p><strong>Revolut</strong>: {t('Investir → Mais → Documentos → Extrato de conta → Excel ou CSV.', 'Invest → More → Documents → Account statement → Excel or CSV.')}</p>
        <p><strong>XTB</strong>: {t('Histórico da conta → Operações de caixa → Exportar. O ficheiro não traz a moeda da conta: o portal assume euros.', 'Account history → Cash operations → Export. The file does not include the account currency: the portal assumes euros.')}</p>
        <p><strong>eToro</strong>: {t('Portefólio → Histórico → ícone do extrato de conta → Excel. O portal lê a folha "Account Activity".', 'Portfolio → History → account statement icon → Excel. The portal reads the "Account Activity" sheet.')}</p>
        <p>
          <strong>Interactive Brokers</strong>: {t('Relatórios → Flex Queries → criar uma consulta em CSV com a secção "Trades" (campos Buy/Sell, TradeDate, ISIN, Description, Quantity, TradePrice, CurrencyPrimary, IBCommission, IBCommissionCurrency, TradeID) e outra com "Cash Transactions" (Type, SettleDate, ISIN, Description, Amount, CurrencyPrimary) para os dividendos. Importa os dois ficheiros.', 'Reports → Flex Queries → create a CSV query with the "Trades" section (fields Buy/Sell, TradeDate, ISIN, Description, Quantity, TradePrice, CurrencyPrimary, IBCommission, IBCommissionCurrency, TradeID) and another with "Cash Transactions" (Type, SettleDate, ISIN, Description, Amount, CurrencyPrimary) for the dividends. Import both files.')}
        </p>
        <p>
          <strong>{t('Outras corretoras', 'Other brokers')}</strong> {t('(Trade Republic, Lightyear…): qualquer CSV ou Excel com as operações serve; confirmas as colunas. Se a corretora só dá PDF, preenche o', '(Trade Republic, Lightyear…): any CSV or Excel with the transactions works; you confirm the columns. If your broker only gives PDFs, fill in the')}
          {' '}<a href="/api/investimentos/modelo.csv" download>{t('modelo em CSV', 'CSV template')}</a> {t('(uma linha por compra, venda ou dividendo).', '(one line per buy, sell or dividend).')}
        </p>
      </Explicacao>
      <Explicacao titulo={t('Posso confiar na leitura automática?', 'Can I trust the automatic reading?')}>
        <p>
          {t('Os leitores de cada corretora foram feitos a partir de ficheiros de exemplo públicos e de conversores open source, não do teu ficheiro. Por isso, depois de importar, compara duas ou três operações com o extrato da corretora (quantidade, preço, data). Se algo não bater certo, apaga as operações importadas e usa a leitura por colunas, ou avisa para o leitor ser corrigido.', 'Each broker reader was built from public sample files and open-source converters, not from your file. So after importing, compare two or three transactions with your broker statement (quantity, price, date). If something does not match, delete the imported transactions and use column mapping, or report it so the reader can be fixed.')}
        </p>
      </Explicacao>

      {analise && mapa && (
        <div className="mapeamento">
          <h3>{t('Confirma as colunas', 'Confirm the columns')}</h3>
          <p className="suave pequeno">
            {t('Não conheço o formato deste ficheiro, por isso tentei adivinhar o que é cada coluna pelo nome. Confirma com as primeiras linhas, mais abaixo: um erro aqui (por exemplo, o preço total em vez do preço por título) muda o imposto.', 'I do not know this file format, so I guessed each column from its name. Check against the first lines below: a mistake here (for example, the total price instead of the price per share) changes the tax.')}
          </p>
          {analise.camposEmFalta.length > 0 && (
            <p className="aviso pequeno"><Icone nome="alerta" tamanho={14} /> {t('Não reconheci:', 'Could not recognise:')} {analise.camposEmFalta.join(', ')}. {t('Escolhe-as abaixo.', 'Choose them below.')}</p>
          )}
          <div className="campos">
            {campos().map(c => (
              <label key={c.chave}>
                {c.nome}{c.obrigatorio && ' *'}
                <select value={(mapa[c.chave] as number | null) ?? ''}
                  onChange={e => mudarColuna(c, e.target.value === '' ? (c.obrigatorio ? -1 : null) : Number(e.target.value))}>
                  <option value="">{c.obrigatorio ? t('— escolher —', '— choose —') : t('— não existe —', '— none —')}</option>
                  {colunas.map((nome, i) => <option key={i} value={i}>{nome}</option>)}
                </select>
                {c.ajuda && <span className="suave pequeno">{c.ajuda}</span>}
              </label>
            ))}
            {mapa.moeda === null && (
              <label>{t('Moeda de todas as operações', 'Currency for every transaction')}
                <input maxLength={3} value={mapa.moedaFixa ?? ''} onChange={e => setMapa({ ...mapa, moedaFixa: e.target.value.toUpperCase() })} />
              </label>
            )}
          </div>

          {mapa.tipo === null ? (
            <label className="linha">
              <input type="checkbox" checked={mapa.vendaSeQuantidadeNegativa} onChange={e => setMapa({ ...mapa, vendaSeQuantidadeNegativa: e.target.checked })} />
              {t('Quantidade negativa = venda (ex.: Degiro)', 'Negative quantity = sell (e.g. Degiro)')}
            </label>
          ) : valoresTipo.length > 0 && (
            <>
              <p className="pequeno"><strong>{t('O que significa cada tipo?', 'What does each type mean?')}</strong></p>
              <p className="suave pequeno">
                {t('Só entram as linhas com Compra, Venda ou Dividendo. Depósitos, levantamentos, juros e câmbios não contam para o Anexo J e ficam em "Ignorar".', 'Only lines marked Buy, Sell or Dividend are imported. Deposits, withdrawals, interest and FX do not count for Annex J and stay on "Ignore".')}
              </p>
              {valoresTipo.length >= maxValoresTipo && (
                <p className="aviso pequeno"><Icone nome="alerta" tamanho={14} /> {t('Esta coluna tem muitos valores diferentes: confirma que é a do tipo de operação.', 'This column has many different values: check it is the transaction type.')}</p>
              )}
              <div className="campos">
                {valoresTipo.map(v => (
                  <label key={v}>“{v}”
                    <select value={mapa.valoresTipo[v] ?? ''}
                      onChange={e => setMapa({ ...mapa, valoresTipo: { ...mapa.valoresTipo, [v]: (e.target.value || null) as TipoOperacao | null } })}>
                      <option value="">{t('Ignorar', 'Ignore')}</option>
                      <option value="Compra">{t('Compra', 'Buy')}</option>
                      <option value="Venda">{t('Venda', 'Sell')}</option>
                      <option value="Dividendo">{t('Dividendo', 'Dividend')}</option>
                    </select>
                  </label>
                ))}
              </div>
            </>
          )}

          <p className="pequeno"><strong>{t('Primeiras linhas do ficheiro', 'First lines of the file')}</strong></p>
          <div className="tabela-rolar">
            <table>
              <thead><tr>{colunas.map((c, i) => <th key={i}>{c}</th>)}</tr></thead>
              <tbody>{analise.amostra.map((l, i) => <tr key={i}>{l.map((v, j) => <td key={j}>{v}</td>)}</tr>)}</tbody>
            </table>
          </div>

          <div className="acoes">
            <button onClick={importar} disabled={!obrigatoriosOk}>{t('Importar', 'Import')}</button>
            <button className="ligacao" onClick={() => setAnalise(null)}>{t('Cancelar', 'Cancel')}</button>
          </div>
        </div>
      )}

      {erro && <p className="erro">{erro}</p>}
      {resultado && (
        <div className={resultado.importadas > 0 ? 'estimativa' : 'aviso pequeno'}>
          {resultado.reconhecido && <>{t('Ficheiro reconhecido:', 'File recognised:')} {resultado.reconhecido}. </>}
          {resultado.importadas} {t('operação(ões) importadas.', 'transaction(s) imported.')}
          {resultado.avisos.length > 0 && <ul className="pequeno">{resultado.avisos.map(a => <li key={a}>{a}</li>)}</ul>}
        </div>
      )}
    </div>
  )
}
