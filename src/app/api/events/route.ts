import { NextResponse } from 'next/server'
import { db } from '@/lib/db'

export async function GET() {
  const events = await db.event.findMany()
  return NextResponse.json(events)
}
