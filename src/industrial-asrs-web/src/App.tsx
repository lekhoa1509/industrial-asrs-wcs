import { useEffect, useMemo, useState } from 'react'
import './App.css'

type Zone = 'A' | 'Charging' | 'B'
type Position = { zone: Zone; rail: number; level: number }
type Device = { deviceId: string; driver: string; state: string; position: Position; activeOrderId?: string; error?: string }
type Order = { orderId: string; source: Position; destination: Position }
type Snapshot = { devices: Device[]; queue: Order[]; events: { timestamp: string; message: string }[]; completed: number; failed: number; activePath: Position[] }

const api = import.meta.env.VITE_API_URL ?? 'http://127.0.0.1:8080'
const rails = Array.from({ length: 12 }, (_, index) => index + 1)
const levels = [5, 4, 3, 2, 1]
const zoneForRail = (rail: number): Zone => rail <= 5 ? 'A' : rail === 6 ? 'Charging' : 'B'
const key = (position: Position) => `${position.rail}-${position.level}`
const storageRails = rails.filter(rail => rail !== 6)
const randomStoragePosition = (): Position => {
  const rail = storageRails[Math.floor(Math.random() * storageRails.length)]
  return { zone: zoneForRail(rail), rail, level: 1 + Math.floor(Math.random() * 5) }
}

export default function App() {
  const [state, setState] = useState<Snapshot>({ devices: [], queue: [], events: [], completed: 0, failed: 0, activePath: [] })
  const activePath = useMemo(() => new Set(state.activePath.map(key)), [state.activePath])
  const refresh = async () => setState(await (await fetch(`${api}/api/state`)).json())

  useEffect(() => {
    refresh()
    const timer = setInterval(refresh, 250)
    return () => clearInterval(timer)
  }, [])

  const createOrder = async () => {
    const source = randomStoragePosition()
    let destination = randomStoragePosition()
    while (key(destination) === key(source)) destination = randomStoragePosition()
    await fetch(`${api}/api/orders`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ orderId: `OUT-${Date.now().toString().slice(-6)}`, source, destination })
    })
    refresh()
  }

  return <main>
    <header>
      <div><p>ERPNext → WCS → SHUTTLE DIGITAL TWIN</p><h1>Warehouse<br />Control System</h1></div>
      <button onClick={createOrder}>CREATE OUTBOUND ORDER <span>+</span></button>
    </header>
    <section className="stats">
      <div><b>{state.devices.length}</b><span>connected shuttles</span></div>
      <div><b>{state.completed}</b><span>completed orders</span></div>
      <div><b>{state.failed}</b><span>failed orders</span></div>
      <div><b>{state.queue.length}</b><span>queued orders</span></div>
    </section>
    <section className="layout">
      <div className="map">
        <div className="map-title"><span>12-RAIL WAREHOUSE TOPOLOGY</span><small>Pink = shuttle track · Green = storage block</small></div>
        <div className="warehouse-shell">
          <div className="zone-strip"><span className="zone-a">ZONE A · RAIL 1–5</span><span className="charging-label">CHARGING · RAIL 6</span><span className="zone-b">ZONE B · RAIL 7–12</span></div>
          <div className="rail-yard">
            {rails.map(rail => {
              const charging = rail === 6
              return <section className={`rail-column ${charging ? 'charging' : ''}`} key={rail}>
                <div className="rail-number">{rail}</div>
                <div className="vertical-track" />
                {charging
                  ? <div className="charger"><i />CHARGE</div>
                  : <div className="rack">{levels.map(level => {
                    const position: Position = { zone: zoneForRail(rail), rail, level }
                    const device = state.devices.find(item => key(item.position) === key(position))
                    return <div className={`block ${activePath.has(key(position)) ? 'active' : ''}`} key={level}>
                      <span>B{level}</span>
                      {device && <div className={`shuttle ${device.state}`}>{device.deviceId}</div>}
                    </div>
                  })}</div>}
                {state.devices.filter(item => item.position.rail === rail && item.position.level === 0).map(device => <div className={`shuttle corridor ${device.state}`} key={device.deviceId}>{device.deviceId}</div>)}
              </section>
            })}
            <div className="main-track" />
            <div className="entry-track"><span>WAREHOUSE ENTRY</span><i /></div>
          </div>
        </div>
      </div>
      <aside>
        <div className="aside-title">TASK QUEUE <span>{state.queue.length}</span></div>
        <div className="queue">{state.queue.length === 0 ? <p className="empty">No waiting orders.<br />Create an outbound order.</p> : state.queue.map(order => <article key={order.orderId}><b>{order.orderId}</b><span>R{order.source.rail}/B{order.source.level} → R{order.destination.rail}/B{order.destination.level}</span></article>)}</div>
        <div className="aside-title">SHUTTLE STATUS</div>
        {state.devices.map(device => <article className="device" key={device.deviceId}><div><b>{device.deviceId}</b><span className={device.state}>{device.state}</span></div><p>{device.position.zone} · Rail {device.position.rail} · {device.position.level === 0 ? 'Main track' : `Block ${device.position.level}`}</p><small>{device.error ?? device.activeOrderId ?? 'Ready for ERPNext order'}</small></article>)}
        <div className="aside-title">WCS EVENT LOG</div>
        <ol>{state.events.slice(0, 7).map((event, index) => <li key={index}>{new Date(event.timestamp).toLocaleTimeString()} {event.message}</li>)}</ol>
      </aside>
    </section>
  </main>
}
