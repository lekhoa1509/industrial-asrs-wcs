import { useEffect, useMemo, useState } from 'react'
import './App.css'

type Position = { zone: 'A' | 'B'; aisle: number; level: number }
type Device = { deviceId: string; driver: string; state: string; position: Position; activeOrderId?: string; error?: string }
type Order = { orderId: string; source: Position; destination: Position }
type Snapshot = { devices: Device[]; queue: Order[]; events: { timestamp: string; message: string }[]; completed: number; failed: number; activePath: Position[] }
const api = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

const cellKey = (p: Position) => `${p.zone}-${p.aisle}-${p.level}`
const randomPosition = (): Position => ({ zone: Math.random() > .5 ? 'A' : 'B', aisle: 1 + Math.floor(Math.random() * 8), level: 1 + Math.floor(Math.random() * 5) })

export default function App() {
  const [state, setState] = useState<Snapshot>({ devices: [], queue: [], events: [], completed: 0, failed: 0, activePath: [] })
  const activeCells = useMemo(() => new Set(state.activePath.map(cellKey)), [state.activePath])
  const refresh = async () => setState(await (await fetch(`${api}/api/state`)).json())
  useEffect(() => { refresh(); const timer = setInterval(refresh, 300); return () => clearInterval(timer) }, [])

  const createOrder = async () => {
    await fetch(`${api}/api/orders`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ orderId: `ORD-${Date.now().toString().slice(-6)}`, source: randomPosition(), destination: randomPosition() }) })
    refresh()
  }

  return <main>
    <header><div><p>INDUSTRIAL AS/RS / DIGITAL TWIN</p><h1>Warehouse<br/>Control System</h1></div><button onClick={createOrder}>CREATE ORDER <span>↗</span></button></header>
    <section className="stats"><div><b>{state.devices.length}</b><span>connected devices</span></div><div><b>{state.completed}</b><span>completed orders</span></div><div><b>{state.failed}</b><span>failed orders</span></div><div><b>{state.queue.length}</b><span>queued orders</span></div></section>
    <section className="layout">
      <div className="map"><div className="map-title"><span>LIVE WAREHOUSE GRID</span><small>Shortest path highlighted in orange</small></div>
        <div className="zones">{(['A','B'] as const).map(zone => <div className="zone" key={zone}><strong>ZONE {zone}</strong><div className="grid">{[5,4,3,2,1].flatMap(level => [1,2,3,4,5,6,7,8].map(aisle => { const p={zone,aisle,level}; const device=state.devices.find(d=>cellKey(d.position)===cellKey(p)); return <div className={`cell ${activeCells.has(cellKey(p))?'path':''}`} key={cellKey(p)}>{device&&<div className={`shuttle ${device.state}`}>{device.deviceId}<i/></div>}<em>{aisle}.{level}</em></div> }))}</div></div>)}</div>
      </div>
      <aside><div className="aside-title">TASK QUEUE <span>{state.queue.length}</span></div><div className="queue">{state.queue.length===0?<p className="empty">No waiting orders.<br/>Create one to start the system.</p>:state.queue.map(o=><article key={o.orderId}><b>{o.orderId}</b><span>{o.source.zone}{o.source.aisle}.{o.source.level} → {o.destination.zone}{o.destination.aisle}.{o.destination.level}</span></article>)}</div>
      <div className="aside-title">DEVICE STATUS</div>{state.devices.map(d=><article className="device" key={d.deviceId}><div><b>{d.deviceId}</b><span className={d.state}>{d.state}</span></div><p>{d.driver} · {d.position.zone}{d.position.aisle}.{d.position.level}</p><small>{d.error??d.activeOrderId??'Ready for dispatch'}</small></article>)}
      <div className="aside-title">EVENT LOG</div><ol>{state.events.slice(0,6).map((e,i)=><li key={i}>{new Date(e.timestamp).toLocaleTimeString()} {e.message}</li>)}</ol></aside>
    </section>
  </main>
}
