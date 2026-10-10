import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { AgGridReact } from 'ag-grid-react'
import type { ColDef, GridApi, GridReadyEvent, IDatasource, IGetRowsParams } from 'ag-grid-community'
import { ArrowUpRight, ChevronDown, ChevronRight, CircleHelp, Download, Filter, Layers3, Search, ShieldCheck, SlidersHorizontal, Sparkles, X } from 'lucide-react'

type RiskPosition = {
  tradeId: string; book: string; commodity: string; counterparty: string; region: string
  instrumentType: string; position: number; currency: string; marketPrice: number
  pnl: number; fxExposure: number; asOfDate: string
}
type Group = { key: string; count: number; pnl: number; fxExposure: number; position: number | null }
type Summary = { rowCount: number; totalPnl: number; totalFxExposure: number; groups: Group[] }
type AiSummaryResponse = { summary: string; sourceRowCount: number }
type FilterModel = Record<string, unknown>

const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
const number = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 })
const apiUrl = '/api/risk-positions'

function formatUsd(value: number) { return money.format(value) }
function signedClass(value: number) { return value < 0 ? 'negative' : 'positive' }

export default function App() {
  const gridApi = useRef<GridApi<RiskPosition> | null>(null)
  const filterRef = useRef<FilterModel>({})
  const searchRef = useRef('')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [summary, setSummary] = useState<Summary | null>(null)
  const [selected, setSelected] = useState<RiskPosition | null>(null)
  const [groupBy, setGroupBy] = useState<'commodity' | 'book'>('commodity')
  const [loadingSummary, setLoadingSummary] = useState(true)
  const [summaryError, setSummaryError] = useState('')
  const [aiSummary, setAiSummary] = useState<AiSummaryResponse | null>(null)
  const [aiSummaryError, setAiSummaryError] = useState('')
  const [isAiLoading, setIsAiLoading] = useState(false)
  const [isAiPanelOpen, setIsAiPanelOpen] = useState(false)
  const [isDetailsOpen, setIsDetailsOpen] = useState(true)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search.trim()), 250)
    return () => window.clearTimeout(timer)
  }, [search])
  useEffect(() => { searchRef.current = debouncedSearch }, [debouncedSearch])

  const loadSummary = useCallback(async (filters: FilterModel, query: string, grouping: string, signal?: AbortSignal) => {
    setLoadingSummary(true)
    setSummaryError('')
    const params = new URLSearchParams({ groupBy: grouping, filterModel: JSON.stringify(filters) })
    if (query) params.set('q', query)
    try {
      const response = await fetch(`${apiUrl}/summary?${params}`, { signal })
      if (!response.ok) throw new Error(`Summary request failed (${response.status})`)
      setSummary(await response.json() as Summary)
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      setSummaryError(error instanceof Error ? error.message : 'Could not load summary')
    } finally {
      if (!signal?.aborted) setLoadingSummary(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void loadSummary(filterRef.current, debouncedSearch, groupBy, controller.signal)
    return () => controller.abort()
  }, [debouncedSearch, groupBy, loadSummary])

  const datasource = useMemo<IDatasource>(() => ({
    getRows: async (params: IGetRowsParams) => {
      const query = new URLSearchParams({
        startRow: String(params.startRow), endRow: String(params.endRow),
        sortModel: JSON.stringify(params.sortModel), filterModel: JSON.stringify(params.filterModel),
      })
      if (searchRef.current) query.set('q', searchRef.current)
      try {
        const response = await fetch(`${apiUrl}?${query}`)
        if (!response.ok) throw new Error(`Data request failed (${response.status})`)
        const result = await response.json() as { rows: RiskPosition[]; totalCount: number }
        params.successCallback(result.rows, result.totalCount)
      } catch {
        params.failCallback()
      }
    },
  }), [])

  const onGridReady = useCallback((event: GridReadyEvent<RiskPosition>) => {
    gridApi.current = event.api
    event.api.setGridOption('datasource', datasource)
  }, [datasource])

  const onFilterChanged = useCallback(() => {
    const filters = gridApi.current?.getFilterModel() ?? {}
    filterRef.current = filters
    void loadSummary(filters, searchRef.current, groupBy)
  }, [groupBy, loadSummary])

  useEffect(() => {
    if (gridApi.current) gridApi.current.refreshInfiniteCache()
  }, [debouncedSearch])

  const columns = useMemo<ColDef<RiskPosition>[]>(() => [
    { field: 'tradeId', headerName: 'Trade ID', pinned: 'left', width: 142, filter: 'agTextColumnFilter' },
    { field: 'book', width: 130, filter: 'agTextColumnFilter' },
    { field: 'commodity', width: 150, filter: 'agTextColumnFilter' },
    { field: 'counterparty', minWidth: 175, flex: 1, filter: 'agTextColumnFilter' },
    { field: 'region', width: 112, filter: 'agTextColumnFilter' },
    { field: 'instrumentType', headerName: 'Instrument', width: 125, filter: 'agTextColumnFilter' },
    { field: 'position', width: 120, type: 'numericColumn', filter: 'agNumberColumnFilter', valueFormatter: p => number.format(p.value ?? 0), cellClass: p => signedClass(p.value ?? 0) },
    { field: 'currency', width: 105, filter: 'agTextColumnFilter' },
    { field: 'marketPrice', headerName: 'Mkt price', width: 125, type: 'numericColumn', filter: 'agNumberColumnFilter', valueFormatter: p => number.format(p.value ?? 0) },
    { field: 'pnl', headerName: 'P&L (USD)', width: 142, type: 'numericColumn', filter: 'agNumberColumnFilter', valueFormatter: p => formatUsd(p.value ?? 0), cellClass: p => signedClass(p.value ?? 0) },
    { field: 'fxExposure', headerName: 'FX exposure', width: 142, type: 'numericColumn', filter: 'agNumberColumnFilter', valueFormatter: p => formatUsd(p.value ?? 0), cellClass: p => signedClass(p.value ?? 0) },
    { field: 'asOfDate', headerName: 'As of', width: 120, filter: 'agDateColumnFilter', filterParams: { comparator: (filterDate: Date, cellValue: string) => {
      if (!cellValue) return -1
      const date = new Date(`${cellValue}T00:00:00`)
      return date < filterDate ? -1 : date > filterDate ? 1 : 0
    } } },
  ], [])

  const defaultColDef = useMemo<ColDef>(() => ({ sortable: true, resizable: true, floatingFilter: true, suppressHeaderMenuButton: true }), [])
  const maxAbsPnl = Math.max(1, ...(summary?.groups.map(group => Math.abs(group.pnl)) ?? [1]))

  const chooseGroup = (group: Group) => {
    if (groupBy === 'commodity') {
      const next = { ...gridApi.current?.getFilterModel(), commodity: { filterType: 'text', type: 'equals', filter: group.key } }
      gridApi.current?.setFilterModel(next)
      filterRef.current = next
      void loadSummary(next, searchRef.current, groupBy)
    } else {
      const next = { ...gridApi.current?.getFilterModel(), book: { filterType: 'text', type: 'equals', filter: group.key } }
      gridApi.current?.setFilterModel(next)
      filterRef.current = next
      void loadSummary(next, searchRef.current, groupBy)
    }
    gridApi.current?.ensureIndexVisible(0, 'top')
  }

  const clearFilters = () => {
    setSearch('')
    gridApi.current?.setFilterModel(null)
    filterRef.current = {}
    setSelected(null)
  }

  const exportCurrentView = async () => {
    const sortModel = gridApi.current?.getColumnState().filter(column => column.sort).map(({ colId, sort }) => ({ colId, sort })) ?? []
    const params = new URLSearchParams({ sortModel: JSON.stringify(sortModel), filterModel: JSON.stringify(gridApi.current?.getFilterModel() ?? filterRef.current) })
    if (searchRef.current) params.set('q', searchRef.current)
    try {
      const response = await fetch(`${apiUrl}/export?${params}`)
      if (!response.ok) throw new Error(`Export request failed (${response.status})`)
      const downloadUrl = URL.createObjectURL(await response.blob())
      const link = document.createElement('a')
      link.href = downloadUrl
      link.download = 'risk-positions.csv'
      link.click()
      window.setTimeout(() => URL.revokeObjectURL(downloadUrl), 1000)
    } catch (error) {
      window.alert(error instanceof Error ? error.message : 'Could not export risk positions')
    }
  }

  const requestAiSummary = async () => {
    setIsAiPanelOpen(true)
    setIsAiLoading(true)
    setAiSummaryError('')
    const filters = gridApi.current?.getFilterModel() ?? filterRef.current
    const params = new URLSearchParams({ groupBy, filterModel: JSON.stringify(filters) })
    if (searchRef.current) params.set('q', searchRef.current)
    try {
      const response = await fetch(`${apiUrl}/summary/ai?${params}`)
      const result = await response.json() as AiSummaryResponse | { message?: string }
      if (!response.ok) throw new Error('message' in result && result.message ? result.message : `AI summary request failed (${response.status})`)
      setAiSummary(result as AiSummaryResponse)
    } catch (error) {
      setAiSummaryError(error instanceof Error ? error.message : 'Unable to generate an AI summary')
    } finally {
      setIsAiLoading(false)
    }
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand"><div className="brand-mark">C</div><span>cargill<span className="brand-dot">.</span></span></div>
        <div className="workspace-label">RISK PLATFORM</div>
        <button className="nav-item active"><Layers3 size={17} /><span>Risk positions</span><span className="nav-indicator" /></button>
        <div className="sidebar-bottom"><div className="secure-icon"><ShieldCheck size={16} /></div><div><strong>Secure workspace</strong><small>Internal use only</small></div></div>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div className="breadcrumbs"><span>Risk</span><ChevronRight size={14} /><strong>Positions</strong></div>
          <div className="topbar-right"><span className="live-dot" /> Market snapshot <span className="topbar-divider" /> <span className="avatar">RM</span><span className="user-name">Risk manager</span></div>
        </header>

        <section className="page-heading">
          <div><div className="eyebrow">ENTERPRISE RISK · DAILY VIEW</div><h1>Risk positions</h1><p>Monitor exposures and P&amp;L across your trading books.</p></div>
          <div className="heading-actions"><span className="as-of-pill"><span className="pill-dot" /> As of 07 Jul 2026</span><button className="button ai-button" onClick={requestAiSummary} disabled={isAiLoading}><Sparkles size={15} /> {isAiLoading ? 'Generating…' : 'AI summary'}</button><button className="button button-secondary" onClick={exportCurrentView}><Download size={15} /> Export view</button></div>
        </section>

        <section className="metric-grid" aria-label="Portfolio summary">
          <article className="metric-card"><div className="metric-label">TOTAL P&amp;L <CircleHelp size={14} /></div><div className={`metric-value ${signedClass(summary?.totalPnl ?? 0)}`}>{loadingSummary && !summary ? '—' : formatUsd(summary?.totalPnl ?? 0)}</div><div className="metric-foot"><span className="metric-caption">Across all matching positions</span><span className="metric-icon green"><ArrowUpRight size={17} /></span></div></article>
          <article className="metric-card"><div className="metric-label">FX EXPOSURE <CircleHelp size={14} /></div><div className={`metric-value ${signedClass(summary?.totalFxExposure ?? 0)}`}>{loadingSummary && !summary ? '—' : formatUsd(summary?.totalFxExposure ?? 0)}</div><div className="metric-foot"><span className="metric-caption">USD equivalent · net exposure</span><span className="metric-icon blue"><SlidersHorizontal size={16} /></span></div></article>
          <article className="metric-card"><div className="metric-label">POSITIONS <CircleHelp size={14} /></div><div className="metric-value">{loadingSummary && !summary ? '—' : (summary?.rowCount ?? 0).toLocaleString()}</div><div className="metric-foot"><span className="metric-caption">Rows matching active filters</span><span className="metric-icon purple"><Layers3 size={16} /></span></div></article>
          <article className="metric-card date-card"><div className="metric-label">VALUATION DATE <CircleHelp size={14} /></div><div className="metric-value date-value">07 <span>JUL</span></div><div className="metric-foot"><span className="metric-caption">Latest available snapshot</span><span className="metric-icon amber"><ChevronDown size={17} /></span></div></article>
        </section>

        {isAiPanelOpen && <section className="ai-summary-card" aria-live="polite">
          <div className="ai-summary-icon"><Sparkles size={17} /></div>
          <div className="ai-summary-body"><div className="ai-summary-title">AI risk briefing <span>· {aiSummary?.sourceRowCount.toLocaleString() ?? summary?.rowCount.toLocaleString() ?? 'filtered'} positions</span></div>
            {isAiLoading ? <p className="ai-summary-copy">Reviewing the filtered risk aggregates…</p> : aiSummaryError ? <p className="ai-summary-error">{aiSummaryError}</p> : <ul className="ai-summary-list">{(aiSummary?.summary ?? '').split(/\r?\n/).map(line => line.replace(/^\s*[-*•]\s*/, '').trim()).filter(Boolean).map((line, index) => <li key={index}>{line}</li>)}</ul>}
          </div>
          <button className="close-detail ai-close" onClick={() => setIsAiPanelOpen(false)} aria-label="Close AI summary"><X size={16} /></button>
        </section>}

        {summaryError && <div className="error-banner">Unable to refresh portfolio totals: {summaryError}</div>}

        <section className="breakdown-card">
          <div className="section-heading"><div><div className="section-title">Exposure breakdown</div><div className="section-subtitle">Aggregated across every row matching the active filters</div></div><div className="segmented-control" aria-label="Group exposures by"><button className={groupBy === 'commodity' ? 'selected' : ''} onClick={() => setGroupBy('commodity')}>Commodity</button><button className={groupBy === 'book' ? 'selected' : ''} onClick={() => setGroupBy('book')}>Book</button></div></div>
          <div className="breakdown-content">
            {(summary?.groups ?? []).slice(0, 6).map(group => <button className="breakdown-row" key={group.key} onClick={() => chooseGroup(group)} title={`Filter positions by ${group.key}`}>
              <span className="group-name">{group.key}<small>{group.count.toLocaleString()} trades</small></span>
              <span className="bar-track"><span className={`bar-fill ${group.pnl < 0 ? 'bar-negative' : ''}`} style={{ width: `${Math.max(2, Math.abs(group.pnl) / maxAbsPnl * 100)}%` }} /></span>
              <span className={`group-pnl ${signedClass(group.pnl)}`}>{formatUsd(group.pnl)}</span><ChevronRight size={15} className="group-chevron" />
            </button>)}
            {!summary?.groups.length && <div className="empty-groups">No matching exposure groups. Adjust your filters to see the breakdown.</div>}
          </div>
          <div className="breakdown-footer"><span>Top {Math.min(6, summary?.groups.length ?? 0)} by absolute P&amp;L</span>{groupBy === 'book' && <span className="unit-note">Position quantities omitted across mixed commodities</span>}</div>
        </section>

        <section className="positions-card">
          <div className="positions-header"><div><div className="section-title">Position detail</div><div className="section-subtitle">Server-side sort, filters and virtual scrolling</div></div><div className="table-actions"><label className="search-box"><Search size={16} /><input value={search} onChange={event => setSearch(event.target.value)} placeholder="Search counterparty, commodity…" /><kbd>⌘ K</kbd></label><button className="icon-button" onClick={clearFilters} title="Clear filters"><Filter size={16} /><span>Clear</span><X size={13} /></button><button className="icon-only" onClick={() => setIsDetailsOpen(!isDetailsOpen)} title={isDetailsOpen ? 'Hide details panel' : 'Show details panel'}>{isDetailsOpen ? <ChevronDown size={17} /> : <ChevronRight size={17} />}</button></div></div>
          <div className={`data-layout ${isDetailsOpen ? '' : 'details-hidden'}`}>
            <div className="grid-wrap ag-theme-quartz"><AgGridReact<RiskPosition>
              theme="legacy"
              columnDefs={columns}
              defaultColDef={defaultColDef}
              rowModelType="infinite"
              cacheBlockSize={100}
              maxBlocksInCache={8}
              infiniteInitialRowCount={100}
              maxConcurrentDatasourceRequests={2}
              datasource={datasource}
              onGridReady={onGridReady}
              onFilterChanged={onFilterChanged}
              onRowClicked={event => setSelected(event.data ?? null)}
              rowSelection="single"
              getRowId={params => params.data.tradeId}
              animateRows={false}
              overlayNoRowsTemplate="<span class='no-rows'>No risk positions match these filters</span>"
            /></div>
            {isDetailsOpen && <aside className="detail-panel">
              <div className="detail-heading"><div><div className="detail-eyebrow">TRADE DRILLDOWN</div><h3>{selected ? selected.tradeId : 'Select a position'}</h3></div>{selected && <button className="close-detail" onClick={() => setSelected(null)} aria-label="Clear selected trade"><X size={16} /></button>}</div>
              {selected ? <><div className="trade-tags"><span>{selected.book}</span><span>{selected.region}</span></div><div className="detail-commodity">{selected.commodity}</div><div className="detail-counterparty">{selected.counterparty}</div><div className="detail-list">
                <Detail label="Instrument" value={selected.instrumentType} /><Detail label="Trade date" value={new Date(`${selected.asOfDate}T00:00:00`).toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' })} /><Detail label="Currency" value={selected.currency} /><Detail label="Position" value={`${number.format(selected.position)} ${selected.commodity}`} /><Detail label="Market price" value={`${selected.currency} ${number.format(selected.marketPrice)}`} /><Detail label="P&L (USD)" value={formatUsd(selected.pnl)} emphasis={selected.pnl < 0} /><Detail label="FX exposure" value={formatUsd(selected.fxExposure)} emphasis={selected.fxExposure < 0} />
              </div><div className="detail-note"><ShieldCheck size={15} /><span>Position-level mark-to-market snapshot</span></div></> : <div className="detail-empty"><div className="empty-icon"><Layers3 size={20} /></div><p>Choose a row in the grid to inspect its trade details, valuation and exposures.</p></div>}
            </aside>}
          </div>
          <div className="table-footer"><span><span className="footer-dot" /> Secure risk data</span><span>Virtualized · 100 rows loaded per request</span></div>
        </section>
        <footer className="page-footer">Corporate Risk Reporting <span>·</span> Data is synthetic and for demonstration only.</footer>
      </main>
    </div>
  )
}

function Detail({ label, value, emphasis = false }: { label: string; value: string; emphasis?: boolean }) {
  return <div className="detail-item"><span>{label}</span><strong className={emphasis ? 'negative' : ''}>{value}</strong></div>
}
