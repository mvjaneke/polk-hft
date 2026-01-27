import { PrismaClient } from '@prisma/client'

const db = new PrismaClient()

async function main() {
  const events = [
    {
      id: '1',
      title: 'Monthly HFT Competition',
      date: new Date(2024, 0, 15), // January 15, 2024
      time: '08:00 - 17:00',
      type: 'competition',
      description: 'Open to all members. Standard HFT rules apply.',
      location: 'Main Course',
      participants: 24,
      maxParticipants: 30
    },
    {
      id: '2',
      title: 'Beginners Training Session',
      date: new Date(2024, 0, 20), // January 20, 2024
      time: '14:00 - 16:00',
      type: 'training',
      description: 'Perfect for new members. Equipment provided.',
      location: 'Training Range',
      participants: 8,
      maxParticipants: 12
    },
    {
      id: '3',
      title: 'Winter League Round 1',
      date: new Date(2024, 0, 27), // January 27, 2024
      time: '09:00 - 13:00',
      type: 'competition',
      description: 'First round of our winter league series.',
      location: 'Main Course',
      participants: 18,
      maxParticipants: 25
    },
    {
      id: '4',
      title: 'Family Fun Day',
      date: new Date(2024, 1, 3), // February 3, 2024
      time: '10:00 - 15:00',
      type: 'social',
      description: 'Bring your family for a fun day of shooting activities.',
      location: 'All Ranges',
      participants: 45,
      maxParticipants: 60
    },
    {
      id: '5',
      title: 'Advanced Techniques Workshop',
      date: new Date(2024, 1, 10), // February 10, 2024
      time: '14:00 - 17:00',
      type: 'training',
      description: 'Learn advanced shooting positions and techniques.',
      location: 'Training Range',
      participants: 6,
      maxParticipants: 8
    },
    {
      id: '6',
      title: 'Club Championship',
      date: new Date(2024, 1, 17), // February 17, 2024
      time: '08:00 - 18:00',
      type: 'competition',
      description: 'Annual club championship with prizes for all categories.',
      location: 'Main Course',
      participants: 32,
      maxParticipants: 40
    }
  ]

  for (const event of events) {
    await db.event.create({
      data: event
    })
  }
}

main()
  .catch(e => {
    console.error(e)
    process.exit(1)
  })
  .finally(async () => {
    await db.$disconnect()
  })
