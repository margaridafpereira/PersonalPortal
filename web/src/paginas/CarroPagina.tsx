import { useEffect, useRef, useState, type FormEvent } from 'react'
import {
  api, dataCurta, euros, nomesCombustivel,
  type CarregamentoConcelho, type Combustivel, type DadosVeiculo, type NovoAbastecimento, type PrazoVeiculo, type PrecosConcelho,
  type Veiculo, type VeiculoCompleto,
} from '../api'
import { Explicacao } from '../componentes/Explicacao'
import { Icone, type NomeIcone } from '../componentes/Icone'
import { lingua, localeDatas, t } from '../i18n'

const iconesPrazo: Record<PrazoVeiculo['tipo'], NomeIcone> = {
  Inspecao: 'certo', Iuc: 'calendario', Seguro: 'escudo', Revisao: 'ajustes', CartaConducao: 'pessoa',
}
const nomesPrazo = (): Record<PrazoVeiculo['tipo'], string> => ({
  Inspecao: t('Inspeção', 'Inspection'), Iuc: 'IUC', Seguro: t('Seguro', 'Insurance'), Revisao: t('Revisão', 'Service'), CartaConducao: t('Carta', 'Licence'),
})

const vazio: DadosVeiculo = {
  nome: '', matricula: null, categoria: 'LigeiroPassageiros', combustivel: 'GasoleoSimples', dataPrimeiraMatricula: '',
  cilindrada: null, emissoesCo2: null, normaCo2: null,
  renovacaoSeguro: null, seguradora: null, apoliceSeguro: null, valorSeguroAnual: null,
  proximaRevisao: null, consumoLitros100Km: null,
}

const num = (v: number, casas = 2) => v.toLocaleString('pt-PT', { minimumFractionDigits: casas, maximumFractionDigits: casas })
const hojeIso = () => new Date().toISOString().slice(0, 10)

export function CarroPagina() {
  const [veiculos, setVeiculos] = useState<VeiculoCompleto[] | null>(null)
  const [concelho, setConcelho] = useState<string | null>(null)
  const [freguesia, setFreguesia] = useState<string | null>(null)
  const [carta, setCarta] = useState<{ validade: string | null; prazo: PrazoVeiculo | null } | null>(null)
  const [editar, setEditar] = useState<Veiculo | 'novo' | null>(null)

  const carregar = () => { api.veiculos().then(setVeiculos); api.carta().then(setCarta) }

  useEffect(() => {
    carregar()
    api.perfil().then(p => { setConcelho(p.concelho); setFreguesia(p.freguesia) })
  }, [])

  if (!veiculos) return <p>{t('A carregar…', 'Loading…')}</p>

  const prazos = [...veiculos.flatMap(v => v.prazos), ...(carta?.prazo && carta.prazo.diasEmFalta >= 0 && carta.prazo.diasEmFalta <= 366 ? [carta.prazo] : [])]
    .sort((a, b) => a.data.localeCompare(b.data))
  // O mesmo veículo que o painel usa: o primeiro a combustível.
  const principal = veiculos.find(v => v.veiculo.combustivel !== 'Eletrico')?.veiculo
  const temEletrico = veiculos.some(v => v.veiculo.combustivel === 'Eletrico')
  const guardado = () => { setEditar(null); carregar() }

  return (
    <>
      <header className="pagina-topo">
        <div>
          <h1>{t('Carro', 'Car')}</h1>
          <p className="suave">{t('Inspeção, IUC, seguro e carta sem esquecer, quanto gastas, e onde abastecer ou carregar mais barato.', 'Inspection, road tax, insurance and licence on time, what you spend, and where to refuel or charge for less.')}</p>
        </div>
        {veiculos.length > 0 && (
          <a className="botao" href={`/api/carro/prazos.ics?idioma=${lingua()}`} download>
            <Icone nome="calendario" tamanho={18} /> {t('Adicionar ao calendário', 'Add to calendar')}
          </a>
        )}
      </header>

      {editar === 'novo' && <FormVeiculo inicial={null} aoGuardar={guardado} aoCancelar={() => setEditar(null)} />}

      {!editar && (
        <button className="botao-grande" onClick={() => setEditar('novo')}>
          <Icone nome="mais" tamanho={20} /> {t('Adicionar veículo', 'Add vehicle')}
        </button>
      )}

      {veiculos.length > 0 && (
        <div className="duas-colunas">
          <section>
            <h2 className="titulo-seccao">{t('Próximos prazos', 'Upcoming deadlines')}</h2>
            <ol className="linha-tempo">
              {prazos.map(p => <LinhaPrazo key={p.veiculoId + p.tipo + p.data} prazo={p} concelho={concelho} />)}
            </ol>

            <h2 className="titulo-seccao">{t('Os teus veículos', 'Your vehicles')}</h2>
            <div className="lista">
              {veiculos.map(v => editar !== 'novo' && editar?.id === v.veiculo.id
                ? <FormVeiculo key={v.veiculo.id} inicial={v.veiculo} aoGuardar={guardado} aoCancelar={() => setEditar(null)} />
                : <CartaoVeiculo key={v.veiculo.id} dados={v} aoEditar={() => setEditar(v.veiculo)} aoMudar={carregar} />)}
            </div>

            {carta && <CartaConducao validade={carta.validade} prazo={carta.prazo} aoMudar={carregar} />}
          </section>

          <section>
            <Combustiveis concelhoPerfil={concelho} freguesiaPerfil={freguesia} inicial={principal?.combustivel ?? (temEletrico ? 'Eletrico' : 'GasoleoSimples')}
              consumo={principal?.consumoLitros100Km ?? null} />
          </section>
        </div>
      )}

      {veiculos.length === 0 && !editar && (
        <p className="vazio">
          {t('Com a data da primeira matrícula (está no documento único do automóvel), a plataforma calcula a data da próxima inspeção e do IUC, e mostra onde o combustível está mais barato no teu concelho.',
            'With the first registration date (on the vehicle registration document), the platform works out your next inspection and road-tax dates, and shows where fuel is cheapest in your municipality.')}
        </p>
      )}
    </>
  )
}

function LinhaPrazo({ prazo: p, concelho }: { prazo: PrazoVeiculo; concelho: string | null }) {
  const d = new Date(p.data + 'T00:00:00')
  return (
    <li className={p.diasEmFalta <= 30 ? 'urgente' : ''}>
      <div className="data-bloco">
        <strong>{d.getDate()}</strong>
        <span>{d.toLocaleDateString(localeDatas(), { month: 'short' }).replace('.', '')}</span>
      </div>
      <div className="prazo-corpo">
        <div className="prazo-titulo">
          <Icone nome={iconesPrazo[p.tipo]} tamanho={16} />
          <strong>{p.titulo}</strong>
          <span className="etiqueta">{nomesPrazo()[p.tipo]}</span>
        </div>
        <p className="suave pequeno">{p.descricao}</p>
        <p className="pequeno ligacoes">
          {p.link && <a href={p.link} target="_blank" rel="noreferrer">{t('Mais informação', 'More information')} <Icone nome="externo" tamanho={12} /></a>}
          {p.tipo === 'Inspecao' && (
            <a href={`https://www.google.com/maps/search/${encodeURIComponent(`centro de inspeção automóvel ${concelho ?? ''}`)}`} target="_blank" rel="noreferrer">
              {t('Centros de inspeção perto de ti', 'Inspection centres near you')} <Icone nome="externo" tamanho={12} />
            </a>
          )}
        </p>
      </div>
      <div className="dias">
        <strong>{p.diasEmFalta}</strong>
        <span>{p.diasEmFalta === 1 ? t('dia', 'day') : t('dias', 'days')}</span>
      </div>
    </li>
  )
}

function CartaoVeiculo({ dados, aoEditar, aoMudar }: { dados: VeiculoCompleto; aoEditar: () => void; aoMudar: () => void }) {
  const { veiculo: v, iuc, consumo } = dados
  const [abastecer, setAbastecer] = useState(false)
  const unidade = v.combustivel === 'Eletrico' ? 'kWh' : 'L'

  const apagar = async () => {
    if (!confirm(t(`Apagar ${v.nome}? Os prazos e os abastecimentos deste veículo deixam de aparecer.`, `Delete ${v.nome}? This vehicle's deadlines and refuels will no longer appear.`))) return
    await api.apagarVeiculo(v.id)
    aoMudar()
  }

  return (
    <article className="cartao veiculo">
      <div className="pesquisa-topo">
        <div>
          <h3>{v.nome} {v.matricula && <span className="matricula">{v.matricula}</span>}</h3>
          <p className="suave pequeno">
            {nomesCombustivel[v.combustivel]} · {t('1.ª matrícula', 'first registered')} {dataCurta(v.dataPrimeiraMatricula)}
            {v.cilindrada ? ` · ${v.cilindrada} cm³` : ''}{v.emissoesCo2 ? ` · ${v.emissoesCo2} g/km CO₂` : ''}
          </p>
        </div>
        <div className="botoes">
          <button className="ligacao" aria-label={`${t('Editar', 'Edit')} ${v.nome}`} onClick={aoEditar}><Icone nome="lapis" tamanho={16} /> {t('Editar', 'Edit')}</button>
          <button className="icone-botao" title={t('Apagar', 'Delete')} aria-label={`${t('Apagar', 'Delete')} ${v.nome}`} onClick={apagar}><Icone nome="lixo" tamanho={18} /></button>
        </div>
      </div>

      <div className="veiculo-blocos">
        <div>
          <span className="suave pequeno">{t('IUC estimado por ano', 'Estimated road tax (IUC) a year')}</span>
          {iuc.valor !== null ? <strong>{euros(iuc.valor)}</strong>
            : iuc.emFalta.length > 0 ? <p className="pequeno">{t('Falta:', 'Missing:')} {iuc.emFalta.join(t(' e ', ' and '))}. <button className="ligacao pequeno" onClick={aoEditar}>{t('Indicar', 'Add')}</button></p>
            : <p className="pequeno">{iuc.aviso}</p>}
          <Explicacao titulo={t('Como é calculado?', 'How is it calculated?')}>
            {iuc.detalhe.length > 0 && <ul>{iuc.detalhe.map(d => <li key={d}>{d}</li>)}</ul>}
            <p>
              {t('Carros matriculados desde julho de 2007 (categoria B) pagam pela cilindrada e pelas emissões de CO₂, vezes um coeficiente do ano; os a gasóleo pagam um adicional. Os anteriores (categoria A) pagam pela cilindrada e pela idade. Os 100% elétricos estão isentos.',
                'Cars registered since July 2007 (category B) pay by engine size and CO₂ emissions, times a year coefficient; diesel cars pay a surcharge. Older cars (category A) pay by engine size and age. Fully electric cars are exempt.')}
            </p>
            <p>
              {t('A cilindrada e o CO₂ estão no certificado de matrícula (campos P.1 e V.7). Taxas de 2024 a 2026, que o Orçamento de 2026 não alterou. É uma estimativa: o valor oficial aparece no',
                'Engine size and CO₂ are on the registration certificate (fields P.1 and V.7). Rates for 2024 to 2026, unchanged by the 2026 Budget. This is an estimate: the official amount is on')} <a href="https://www.portaldasfinancas.gov.pt/pt/menu.action?pai=5225" target="_blank" rel="noreferrer">Portal das Finanças</a>.
            </p>
            {iuc.aviso && iuc.valor !== null && <p>{iuc.aviso}</p>}
          </Explicacao>
        </div>

        <div>
          <span className="suave pequeno">{t('Seguro', 'Insurance')}</span>
          {v.seguradora || v.valorSeguroAnual ? (
            <p className="pequeno">
              <strong>{v.seguradora ?? t('Seguradora por indicar', 'Insurer not set')}</strong>
              {v.valorSeguroAnual ? ` · ${euros(v.valorSeguroAnual)}${t('/ano', '/year')}` : ''}
              {v.renovacaoSeguro ? ` · ${t('renova a', 'renews on')} ${dataCurta(v.renovacaoSeguro).slice(0, 6)}` : ''}
              {v.apoliceSeguro ? <><br /><span className="suave">{t('Apólice', 'Policy')} {v.apoliceSeguro}</span></> : null}
            </p>
          ) : <p className="pequeno"><button className="ligacao pequeno" onClick={aoEditar}>{t('Indicar o seguro', 'Add the insurance')}</button> {t('para teres a renovação nos prazos.', 'to see the renewal in your deadlines.')}</p>}
        </div>

        <div>
          <span className="suave pequeno">{t('Consumo real', 'Real consumption')}</span>
          {consumo.consumoReal !== null ? (
            <p className="pequeno">
              <strong>{num(consumo.consumoReal, 1)} {unidade}/100 km</strong>
              {consumo.custoPorKm !== null && ` · ${num(consumo.custoPorKm, 3)} €/km`}
              {consumo.gastoMensal !== null && <><br />{euros(consumo.gastoMensal)} {t('por mês em média', 'a month on average')}</>}
            </p>
          ) : <p className="pequeno suave">{t('Regista dois depósitos cheios para calcular.', 'Log two full tanks to calculate it.')}</p>}
          <Explicacao titulo={t('Como se calcula?', 'How is it calculated?')}>
            <p>
              {t('Enche o depósito e regista o abastecimento com os quilómetros do conta-quilómetros. No depósito cheio seguinte, os litros postos são os que gastaste nesses quilómetros. Abastecimentos parciais pelo meio também contam.',
                'Fill the tank and log the refuel with the odometer reading. At the next full tank, the litres you put in are what you used over those kilometres. Partial refuels in between count too.')}
            </p>
          </Explicacao>
        </div>
      </div>

      <div className="abastecimentos">
        <div className="pesquisa-topo">
          <strong className="pequeno">{t('Abastecimentos', 'Refuels')} {consumo.abastecimentos > 0 && <span className="suave">({consumo.abastecimentos})</span>}</strong>
          {!abastecer && <button className="ligacao pequeno" onClick={() => setAbastecer(true)}><Icone nome="mais" tamanho={14} /> {t('Registar', 'Log')}</button>}
        </div>
        {abastecer && <FormAbastecimento veiculoId={v.id} unidade={unidade} aoGuardar={() => { setAbastecer(false); aoMudar() }} aoCancelar={() => setAbastecer(false)} />}
        {dados.abastecimentos.length > 0 && (
          <table className="pequeno">
            <thead><tr><th>{t('Data', 'Date')}</th><th className="num">km</th><th className="num">{unidade}</th><th className="num">{t('Valor', 'Amount')}</th><th /></tr></thead>
            <tbody>
              {dados.abastecimentos.map(a => (
                <tr key={a.id}>
                  <td>{dataCurta(a.data)}{!a.depositoCheio && <span className="suave"> {t('(parcial)', '(partial)')}</span>}</td>
                  <td className="num">{a.quilometros.toLocaleString('pt-PT')}</td>
                  <td className="num">{num(a.litros)}</td>
                  <td className="num">{euros(a.valorTotal)}</td>
                  <td><button className="icone-botao" aria-label={t('Apagar abastecimento', 'Delete refuel')} onClick={async () => { await api.apagarAbastecimento(a.id); aoMudar() }}><Icone nome="lixo" tamanho={14} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </article>
  )
}

function FormAbastecimento({ veiculoId, unidade, aoGuardar, aoCancelar }: { veiculoId: string; unidade: string; aoGuardar: () => void; aoCancelar: () => void }) {
  const [a, setA] = useState<NovoAbastecimento>({ data: hojeIso(), quilometros: 0, litros: 0, valorTotal: 0, depositoCheio: true, posto: null })
  const [erro, setErro] = useState<string | null>(null)
  const n = (v: string) => (v === '' ? 0 : Number(v))

  const submeter = async (e: FormEvent) => {
    e.preventDefault()
    try {
      await api.novoAbastecimento(veiculoId, a)
      aoGuardar()
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  return (
    <form className="formulario-linha" onSubmit={submeter}>
      <div className="campos">
        <label>{t('Data', 'Date')}<input type="date" required max={hojeIso()} value={a.data} onChange={e => setA({ ...a, data: e.target.value })} /></label>
        <label>{t('Quilómetros', 'Kilometres')}<input type="number" required min={0} value={a.quilometros || ''} onChange={e => setA({ ...a, quilometros: n(e.target.value) })} /></label>
        <label>{unidade === 'kWh' ? 'kWh' : t('Litros', 'Litres')}<input type="number" required min={0} step="any" value={a.litros || ''} onChange={e => setA({ ...a, litros: n(e.target.value) })} /></label>
        <label>{t('Valor pago (€)', 'Amount paid (€)')}<input type="number" required min={0} step="any" value={a.valorTotal || ''} onChange={e => setA({ ...a, valorTotal: n(e.target.value) })} /></label>
        <label>{t('Posto (opcional)', 'Station (optional)')}<input value={a.posto ?? ''} onChange={e => setA({ ...a, posto: e.target.value || null })} /></label>
      </div>
      <label className="linha pequeno">
        <input type="checkbox" checked={a.depositoCheio} onChange={e => setA({ ...a, depositoCheio: e.target.checked })} /> {t('Enchi o depósito', 'I filled the tank')}
      </label>
      {erro && <p className="erro">{erro}</p>}
      <div className="acoes">
        <button type="submit">{t('Guardar', 'Save')}</button>
        <button type="button" className="ligacao" onClick={aoCancelar}>{t('Cancelar', 'Cancel')}</button>
      </div>
    </form>
  )
}

function CartaConducao({ validade, prazo, aoMudar }: { validade: string | null; prazo: PrazoVeiculo | null; aoMudar: () => void }) {
  const [data, setData] = useState(validade ?? '')
  const [guardado, setGuardado] = useState(false)

  const guardar = async (e: FormEvent) => {
    e.preventDefault()
    await api.guardarCarta(data || null)
    setGuardado(true)
    aoMudar()
  }

  return (
    <>
      <h2 className="titulo-seccao">{t('Carta de condução', 'Driving licence')}</h2>
      <form className="cartao formulario-linha" onSubmit={guardar}>
        <label>{t('Válida até (campo 4b da carta)', 'Valid until (field 4b of the licence)')}
          <input type="date" value={data} onChange={e => { setData(e.target.value); setGuardado(false) }} />
        </label>
        {prazo && (
          <p className="pequeno">
            {validade ? t('Revalidação', 'Renewal') : t('Revalidação provável', 'Likely renewal')}: <strong>{dataCurta(prazo.data)}</strong>
            {prazo.diasEmFalta >= 0 ? t(` (daqui a ${prazo.diasEmFalta} dias)`, ` (in ${prazo.diasEmFalta} days)`) : t(' (já passou: confirma a validade)', ' (already passed: check the expiry date)')}
          </p>
        )}
        <Explicacao>
          <p>
            {t('A carta de condução de ligeiros (grupo 1) revalida-se aos 30, 40, 50, 60, 65 e 70 anos e depois de dois em dois anos; a partir dos 60 é preciso atestado médico. Podes pedir a revalidação até 6 meses antes, no IMT Online. Sem a data da carta, o portal estima-a pela tua data de nascimento.',
              'A car driving licence (group 1) is renewed at 30, 40, 50, 60, 65 and 70, then every two years; from 60 a medical certificate is required. You can apply up to 6 months early at IMT Online. Without the licence date, the portal estimates it from your date of birth.')}
          </p>
        </Explicacao>
        <div className="acoes">
          <button type="submit">{t('Guardar', 'Save')}</button>
          {guardado && <span className="pequeno" role="status">{t('Guardado.', 'Saved.')}</span>}
        </div>
      </form>
    </>
  )
}

const mesmoNome = (a: string, b: string) => a.localeCompare(b, 'pt', { sensitivity: 'base' }) === 0

function Combustiveis({ concelhoPerfil, freguesiaPerfil, inicial, consumo }: {
  concelhoPerfil: string | null; freguesiaPerfil: string | null; inicial: Combustivel; consumo: number | null
}) {
  const [combustivel, setCombustivel] = useState<Combustivel>(inicial)
  const [escolhido, setEscolhido] = useState<string | null>(null)
  const [escrito, setEscrito] = useState<string | null>(null)
  const [concelhos, setConcelhos] = useState<string[]>([])
  // undefined = ainda não escolhida (usa a freguesia do perfil, se existir no concelho); null = o concelho todo.
  const [localidade, setLocalidade] = useState<string | null | undefined>(undefined)
  const [localidades, setLocalidades] = useState<{ concelho: string; lista: string[] } | null>(null)
  const [resposta, setResposta] = useState<{ chave: string; precos: PrecosConcelho | null } | null>(null)
  // Sem escolha, vale o concelho do perfil (que chega depois do primeiro render).
  const concelho = escolhido ?? concelhoPerfil
  const lista = localidades?.concelho === concelho ? localidades.lista : []
  const localidadeEfetiva = localidade !== undefined ? localidade
    : concelho === concelhoPerfil && freguesiaPerfil ? lista.find(l => mesmoNome(l, freguesiaPerfil)) ?? null : null
  const chave = `${concelho}|${combustivel}|${localidadeEfetiva ?? ''}`

  useEffect(() => { api.concelhos().then(setConcelhos, () => setConcelhos([])) }, [])

  // A lista de localidades vem da resposta do concelho todo (e mantém-se ao filtrar).
  useEffect(() => {
    if (!concelho || combustivel === 'Eletrico') return
    api.combustiveis(concelho, combustivel).then(p => { if (p?.localidades) setLocalidades({ concelho, lista: p.localidades }) })
  }, [concelho, combustivel])

  useEffect(() => {
    if (!concelho || combustivel === 'Eletrico') return
    api.combustiveis(concelho, combustivel, localidadeEfetiva)
      .then(precos => setResposta({ chave: `${concelho}|${combustivel}|${localidadeEfetiva ?? ''}`, precos }))
  }, [concelho, combustivel, localidadeEfetiva])

  const escrever = (texto: string) => {
    setEscrito(texto)
    // Só procura quando o texto é um concelho da lista (evita um pedido por cada letra).
    const encontrado = concelhos.find(c => mesmoNome(c, texto))
    if (encontrado && encontrado !== escolhido) { setEscolhido(encontrado); setLocalidade(undefined) }
  }

  const precos = resposta?.chave === chave ? resposta.precos : undefined
  const poupancaDeposito = precos ? (precos.media - precos.minimo) * 50 : 0

  return (
    <div className="cartao combustiveis">
      <h2><Icone nome="lupa" tamanho={18} /> {combustivel === 'Eletrico' ? t('Carregar', 'Charge') : t('Abastecer', 'Refuel')} {t('em', 'in')} {localidadeEfetiva ?? concelho ?? '—'}</h2>
      <div className="campos">
        <label>{t('Concelho', 'Municipality')}
          <input list="concelhos-dgeg" value={escrito ?? concelho ?? ''} placeholder={t('Escreve o concelho', 'Type the municipality')} onChange={e => escrever(e.target.value)} />
          <datalist id="concelhos-dgeg">{concelhos.map(c => <option key={c} value={c} />)}</datalist>
        </label>
        {combustivel !== 'Eletrico' && lista.length > 1 && (
          <label>{t('Freguesia / localidade', 'Parish / locality')}
            <select value={localidadeEfetiva ?? ''} onChange={e => setLocalidade(e.target.value || null)}>
              <option value="">{t('Todo o concelho', 'Whole municipality')}</option>
              {lista.map(l => <option key={l} value={l}>{l}</option>)}
            </select>
          </label>
        )}
        <label>{t('Combustível', 'Fuel')}
          <select value={combustivel} onChange={e => setCombustivel(e.target.value as Combustivel)}>
            {Object.entries(nomesCombustivel).map(([k, v]) => <option key={k} value={k}>{k === 'Eletrico' ? t('Elétrico (carregamento)', 'Electric (charging)') : v}</option>)}
          </select>
        </label>
      </div>
      {concelhoPerfil && concelho !== concelhoPerfil && (
        <p className="pequeno"><button className="ligacao pequeno" onClick={() => { setEscolhido(null); setEscrito(null); setLocalidade(undefined) }}>{t('Voltar a', 'Back to')} {concelhoPerfil}</button></p>
      )}
      {!concelho && <p className="aviso pequeno">{t('Escolhe um concelho, ou indica o teu no', 'Choose a municipality, or add yours to your')} <a href="#/perfil">{t('perfil', 'profile')}</a>.</p>}

      {combustivel === 'Eletrico' ? (concelho && <Carregamento concelho={concelho} />) : (
        <>
          {precos === undefined && concelho && <p className="suave">{t('A consultar a DGEG…', 'Checking DGEG…')}</p>}
          {precos === null && concelho && <p className="suave">{t('Sem postos com este combustível', 'No stations with this fuel')} {localidadeEfetiva ? `${t('em', 'in')} ${localidadeEfetiva}` : t('no concelho', 'in the municipality')}.</p>}
          {precos && (
            <>
              <div className="indicadores">
                <div className="indicador tom-positivo"><strong>{precos.minimo.toFixed(3).replace('.', ',')} €</strong><span>{t('mais barato', 'cheapest')}</span></div>
                <div className="indicador"><strong>{precos.media.toFixed(3).replace('.', ',')} €</strong><span>{t(`média de ${precos.numeroPostos} postos`, `average of ${precos.numeroPostos} stations`)}</span></div>
              </div>
              <p className="pequeno">
                {t('Encher 50 L no mais barato poupa', 'Filling 50 L at the cheapest saves')} <strong>{poupancaDeposito.toFixed(2).replace('.', ',')} €</strong> {t('face à média.', 'compared with the average.')}
                {consumo ? t(` Com ${consumo} L/100 km, são cerca de ${((precos.media - precos.minimo) * consumo * 150).toFixed(0)} € por ano (15 000 km).`, ` At ${consumo} L/100 km, that is about ${((precos.media - precos.minimo) * consumo * 150).toFixed(0)} € a year (15,000 km).`) : ''}
              </p>
              <ol className="postos">
                {precos.maisBaratos.map((p, i) => (
                  <li key={i}>
                    <div>
                      <strong>{p.nome}</strong> <span className="etiqueta">{p.marca}</span>
                      <p className="suave pequeno">{p.morada}, {p.localidade}</p>
                    </div>
                    <div className="posto-preco">
                      <strong>{p.preco.toFixed(3).replace('.', ',')} €</strong>
                      {p.latitude && p.longitude && (
                        <a className="pequeno" href={`https://www.google.com/maps/search/?api=1&query=${p.latitude},${p.longitude}`} target="_blank" rel="noreferrer">
                          {t('Mapa', 'Map')} <Icone nome="externo" tamanho={12} />
                        </a>
                      )}
                    </div>
                  </li>
                ))}
              </ol>
              <p className="suave pequeno">
                {t('Fonte: DGEG, preços comunicados pelos postos. A localidade é a que cada posto indica à DGEG; normalmente coincide com a freguesia ou a vila.', 'Source: DGEG, prices reported by the stations. The locality is the one each station gives DGEG; it usually matches the parish or town.')}
              </p>
            </>
          )}
        </>
      )}
    </div>
  )
}

function Carregamento({ concelho }: { concelho: string }) {
  const [resposta, setResposta] = useState<{ concelho: string; dados: CarregamentoConcelho | null } | null>(null)

  useEffect(() => { api.carregamento(concelho).then(dados => setResposta({ concelho, dados })) }, [concelho])

  const dados = resposta?.concelho === concelho ? resposta.dados : undefined
  if (dados === undefined) return <p className="suave">{t('A consultar a Mobi.E (o ficheiro de tarifas é grande; a primeira vez demora)…', 'Checking Mobi.E (the tariff file is large; the first time takes a while)…')}</p>
  if (dados === null) return <p className="suave">{t('Sem postos Mobi.E conhecidos neste concelho.', 'No known Mobi.E chargers in this municipality.')}</p>

  return (
    <>
      <p className="pequeno">{t(`${dados.numeroPostos} postos Mobi.E. Os mais baratos para carregar ${dados.energia} kWh, pela tarifa do operador do posto:`, `${dados.numeroPostos} Mobi.E chargers. The cheapest to charge ${dados.energia} kWh, by the charge-point operator's tariff:`)}</p>
      <ol className="postos">
        {dados.maisBaratos.map(p => (
          <li key={p.id}>
            <div>
              <strong>{p.morada}</strong> <span className="etiqueta">{p.operador}</span>
              <p className="suave pequeno">{p.tipo} · {num(p.potenciaKw, 1)} kW · {p.tarifas.join(' + ')}</p>
            </div>
            <div className="posto-preco">
              <strong>{euros(p.custoOperador)}</strong>
              <a className="pequeno" href={`https://www.google.com/maps/search/${encodeURIComponent(`${p.morada}, ${dados.concelho}`)}`} target="_blank" rel="noreferrer">
                {t('Mapa', 'Map')} <Icone nome="externo" tamanho={12} />
              </a>
            </div>
          </li>
        ))}
      </ol>
      <Explicacao titulo={t('Porque é que este não é o preço final?', 'Why is this not the final price?')}>
        <p>
          {t('Na rede Mobi.E pagas duas coisas: a tarifa do operador do posto (OPC), que é a que aparece aqui, e a energia ao teu comercializador (CEME, a empresa do teu cartão ou app de carregamento), mais impostos. A parte da energia depende do teu contrato, por isso não entra na comparação.',
            'On the Mobi.E network you pay two things: the charge-point operator tariff (OPC), shown here, and the energy to your supplier (CEME, the company behind your card or charging app), plus taxes. The energy part depends on your contract, so it is not in the comparison.')}
        </p>
        <p>{t('O tempo de carregamento é estimado pela potência da tomada (até 50 kW). Fonte: ficheiro público de tarifas da Mobi.E, atualizado pelos operadores.', 'Charging time is estimated from the socket power (up to 50 kW). Source: the public Mobi.E tariff file, updated by the operators.')}</p>
      </Explicacao>
    </>
  )
}

function FormVeiculo({ inicial, aoGuardar, aoCancelar }: { inicial: Veiculo | null; aoGuardar: () => void; aoCancelar: () => void }) {
  const [v, setV] = useState<DadosVeiculo>(inicial ?? vazio)
  const [erro, setErro] = useState<string | null>(null)
  const formulario = useRef<HTMLFormElement>(null)
  const inteiro = (valor: string) => (valor === '' ? null : Math.round(Number(valor)))
  const decimal = (valor: string) => (valor === '' ? null : Number(valor))

  // O formulário pode abrir fora do ecrã (ex.: ao editar um veículo lá em baixo): leva a pessoa até ele.
  useEffect(() => {
    formulario.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    formulario.current?.querySelector('input')?.focus({ preventScroll: true })
  }, [])

  const submeter = async (e: FormEvent) => {
    e.preventDefault()
    try {
      if (inicial) await api.alterarVeiculo(inicial.id, v)
      else await api.criarVeiculo(v)
      aoGuardar()
    } catch (err) {
      setErro((err as Error).message)
    }
  }

  return (
    <form ref={formulario} className="cartao formulario-linha" onSubmit={submeter}>
      <h2><Icone nome={inicial ? 'lapis' : 'mais'} tamanho={18} /> {inicial ? `${t('Editar', 'Edit')} ${inicial.nome}` : t('Novo veículo', 'New vehicle')}</h2>
      <div className="campos">
        <label>{t('Nome', 'Name')}
          <input required placeholder={t('ex.: Clio', 'e.g. Clio')} value={v.nome} onChange={e => setV({ ...v, nome: e.target.value })} />
        </label>
        <label>{t('Matrícula (opcional)', 'Plate (optional)')}
          <input placeholder="AA-00-BB" value={v.matricula ?? ''} onChange={e => setV({ ...v, matricula: e.target.value || null })} />
        </label>
        <label>{t('Data da 1.ª matrícula', 'First registration date')}
          <input type="date" required value={v.dataPrimeiraMatricula} onChange={e => setV({ ...v, dataPrimeiraMatricula: e.target.value })} />
        </label>
        <label>{t('Tipo', 'Type')}
          <select value={v.categoria} onChange={e => setV({ ...v, categoria: e.target.value as DadosVeiculo['categoria'] })}>
            <option value="LigeiroPassageiros">{t('Ligeiro de passageiros', 'Passenger car')}</option>
            <option value="LigeiroMercadorias">{t('Ligeiro de mercadorias', 'Light goods vehicle')}</option>
          </select>
        </label>
        <label>{t('Combustível', 'Fuel')}
          <select value={v.combustivel} onChange={e => setV({ ...v, combustivel: e.target.value as Combustivel })}>
            {Object.entries(nomesCombustivel).map(([k, n]) => <option key={k} value={k}>{n}</option>)}
          </select>
        </label>
        <label>{t('Consumo médio (L/100 km)', 'Average consumption (L/100 km)')}
          <input type="number" min={0} step={0.1} value={v.consumoLitros100Km ?? ''} onChange={e => setV({ ...v, consumoLitros100Km: decimal(e.target.value) })} />
        </label>
      </div>

      <h3 className="pequeno">{t('Para estimar o IUC', 'To estimate road tax (IUC)')} <span className="suave">{t('(certificado de matrícula)', '(registration certificate)')}</span></h3>
      <div className="campos">
        <label>{t('Cilindrada (cm³, campo P.1)', 'Engine size (cm³, field P.1)')}
          <input type="number" min={0} max={10000} value={v.cilindrada ?? ''} onChange={e => setV({ ...v, cilindrada: inteiro(e.target.value) })} />
        </label>
        <label>{t('CO₂ (g/km, campo V.7)', 'CO₂ (g/km, field V.7)')}
          <input type="number" min={0} max={1000} value={v.emissoesCo2 ?? ''} onChange={e => setV({ ...v, emissoesCo2: inteiro(e.target.value) })} />
        </label>
        <label>{t('Norma do CO₂', 'CO₂ standard')}
          <select value={v.normaCo2 ?? ''} onChange={e => setV({ ...v, normaCo2: (e.target.value || null) as DadosVeiculo['normaCo2'] })}>
            <option value="">{t('Não sei (o portal deduz pela data)', 'Not sure (the portal works it out from the date)')}</option>
            <option value="Wltp">{t('WLTP (carros mais recentes)', 'WLTP (newer cars)')}</option>
            <option value="Nedc">NEDC</option>
          </select>
        </label>
      </div>

      <h3 className="pequeno">{t('Seguro e revisão', 'Insurance and service')}</h3>
      <div className="campos">
        <label>{t('Seguradora', 'Insurer')}
          <input value={v.seguradora ?? ''} onChange={e => setV({ ...v, seguradora: e.target.value || null })} />
        </label>
        <label>{t('N.º da apólice', 'Policy number')}
          <input value={v.apoliceSeguro ?? ''} onChange={e => setV({ ...v, apoliceSeguro: e.target.value || null })} />
        </label>
        <label>{t('Valor anual (€)', 'Annual premium (€)')}
          <input type="number" min={0} step="any" value={v.valorSeguroAnual ?? ''} onChange={e => setV({ ...v, valorSeguroAnual: decimal(e.target.value) })} />
        </label>
        <label>{t('Renovação do seguro', 'Insurance renewal')}
          <input type="date" value={v.renovacaoSeguro ?? ''} onChange={e => setV({ ...v, renovacaoSeguro: e.target.value || null })} />
        </label>
        <label>{t('Próxima revisão', 'Next service')}
          <input type="date" value={v.proximaRevisao ?? ''} onChange={e => setV({ ...v, proximaRevisao: e.target.value || null })} />
        </label>
      </div>
      {erro && <p className="erro">{erro}</p>}
      <div className="acoes">
        <button type="submit">{t('Guardar', 'Save')}</button>
        <button type="button" className="ligacao" onClick={aoCancelar}>{t('Cancelar', 'Cancel')}</button>
      </div>
    </form>
  )
}
