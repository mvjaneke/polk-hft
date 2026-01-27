import { PrismaClient } from '@prisma/client'
import { PrismaBetterSqlite3Adapter } from '@prisma/adapter-better-sqlite3'
import betterSqlite3 from 'better-sqlite3'

const sqlite = new betterSqlite3('../db/custom.db')
const adapter = new PrismaBetterSqlite3Adapter(sqlite)

const globalForPrisma = globalThis as unknown as {
  prisma: PrismaClient | undefined
}

export const db =
  globalForPrisma.prisma ??
  new PrismaClient({
    adapter,
    log: ['query'],
  })

if (process.env.NODE_ENV !== 'production') globalForPrisma.prisma = db