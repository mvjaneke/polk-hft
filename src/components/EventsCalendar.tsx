'use client'

import { useState, useEffect } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Calendar, Clock, MapPin, Trophy, Users, Target, ChevronLeft, ChevronRight } from 'lucide-react'

interface Event {
  id: string
  title: string
  date: Date
  time: string
  type: 'competition' | 'training' | 'social'
  description: string
  location?: string | null
  participants?: number | null
  maxParticipants?: number | null
}

export default function EventsCalendar() {
  const [events, setEvents] = useState<Event[]>([])
  const [currentMonth, setCurrentMonth] = useState(new Date())

  useEffect(() => {
    const fetchEvents = async () => {
      const res = await fetch('/api/events')
      const data = await res.json()
      setEvents(data.map((event: any) => ({ ...event, date: new Date(event.date) })))
    }
    fetchEvents()
  }, [])

  const getDaysInMonth = (date: Date) => {
    const year = date.getFullYear()
    const month = date.getMonth()
    const firstDay = new Date(year, month, 1)
    const lastDay = new Date(year, month + 1, 0)
    const daysInMonth = lastDay.getDate()
    const startingDayOfWeek = firstDay.getDay()

    const days = []
    for (let i = 0; i < startingDayOfWeek; i++) {
      days.push(null)
    }
    for (let i = 1; i <= daysInMonth; i++) {
      days.push(i)
    }
    return days
  }

  const getEventsForDate = (day: number) => {
    return events.filter(event => {
      const eventDate = new Date(event.date)
      return eventDate.getDate() === day &&
             eventDate.getMonth() === currentMonth.getMonth() &&
             eventDate.getFullYear() === currentMonth.getFullYear()
    })
  }

  const navigateMonth = (direction: 'prev' | 'next') => {
    setCurrentMonth(prev => {
      const newDate = new Date(prev)
      if (direction === 'prev') {
        newDate.setMonth(prev.getMonth() - 1)
      } else {
        newDate.setMonth(prev.getMonth() + 1)
      }
      return newDate
    })
  }

  const getEventTypeColor = (type: Event['type']) => {
    switch (type) {
      case 'competition':
        return 'bg-red-100 text-red-800 border-red-200'
      case 'training':
        return 'bg-gray-100 text-gray-800 border-gray-200'
      case 'social':
        return 'bg-gray-100 text-gray-800 border-gray-200'
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200'
    }
  }

  const getEventTypeIcon = (type: Event['type']) => {
    switch (type) {
      case 'competition':
        return <Trophy className="w-4 h-4" />
      case 'training':
        return <Target className="w-4 h-4" />
      case 'social':
        return <Users className="w-4 h-4" />
      default:
        return <Calendar className="w-4 h-4" />
    }
  }

  const monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ]

  const days = getDaysInMonth(currentMonth)

  return (
    <div className="space-y-8">
      {/* Calendar Grid */}
      <Card className="bg-white/80 backdrop-blur-sm border-gray-300 shadow-md">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-2xl text-gray-900">
              {monthNames[currentMonth.getMonth()]} {currentMonth.getFullYear()}
            </CardTitle>
            <div className="flex space-x-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => navigateMonth('prev')}
                className="border-gray-300 text-gray-700 hover:bg-gray-50"
              >
                <ChevronLeft className="w-4 h-4" />
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => navigateMonth('next')}
                className="border-gray-300 text-gray-700 hover:bg-gray-50"
              >
                <ChevronRight className="w-4 h-4" />
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-7 gap-2 mb-4">
            {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map(day => (
              <div key={day} className="text-center text-sm font-semibold text-gray-600 py-2">
                {day}
              </div>
            ))}
          </div>
          <div className="grid grid-cols-7 gap-2">
            {days.map((day, index) => {
              const dayEvents = day ? getEventsForDate(day) : []
              const hasEvents = dayEvents.length > 0

              return (
                <div
                  key={index}
                  className={`min-h-[80px] p-2 border rounded-lg ${
                    day ? 'border-gray-300 hover:bg-gray-50' : 'border-transparent'
                  } ${hasEvents ? 'bg-red-50' : ''}`}
                >
                  {day && (
                    <div className="space-y-1">
                      <div className="text-sm font-medium text-gray-700">{day}</div>
                      {dayEvents.slice(0, 2).map(event => (
                        <div
                          key={event.id}
                          className={`text-xs p-1 rounded ${getEventTypeColor(event.type)}`}
                        >
                          {event.title.split(' ').slice(0, 2).join(' ')}...
                        </div>
                      ))}
                      {dayEvents.length > 2 && (
                        <div className="text-xs text-gray-500">+{dayEvents.length - 2} more</div>
                      )}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </CardContent>
      </Card>

      {/* Upcoming Events List */}
      <div className="grid md:grid-cols-2 gap-6">
        <Card className="bg-white/80 backdrop-blur-sm border-gray-300 shadow-md">
          <CardHeader>
            <CardTitle className="text-xl text-gray-900">Upcoming Events</CardTitle>
            <CardDescription>Don't miss these upcoming activities</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {events
              .filter(event => event.date >= new Date())
              .sort((a, b) => a.date.getTime() - b.date.getTime())
              .slice(0, 4)
              .map(event => (
                <div key={event.id} className="border border-gray-300 rounded-lg p-4 hover:bg-red-50 transition-colors">
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex-1">
                      <h4 className="font-semibold text-gray-900">{event.title}</h4>
                      <div className="flex items-center space-x-4 text-sm text-gray-600 mt-1">
                        <div className="flex items-center space-x-1">
                          <Calendar className="w-4 h-4" />
                          <span>{event.date.toLocaleDateString()}</span>
                        </div>
                        <div className="flex items-center space-x-1">
                          <Clock className="w-4 h-4" />
                          <span>{event.time}</span>
                        </div>
                      </div>
                    </div>
                    <Badge className={getEventTypeColor(event.type)}>
                      <div className="flex items-center space-x-1">
                        {getEventTypeIcon(event.type)}
                        <span className="capitalize">{event.type}</span>
                      </div>
                    </Badge>
                  </div>
                  <p className="text-sm text-gray-700 mb-2">{event.description}</p>
                  <div className="flex items-center justify-between">
                    {event.location && (
                      <div className="flex items-center space-x-1 text-sm text-gray-600">
                        <MapPin className="w-4 h-4" />
                        <span>{event.location}</span>
                      </div>
                    )}
                    {event.participants && (
                      <div className="flex items-center space-x-1 text-sm text-gray-600">
                        <Users className="w-4 h-4" />
                        <span>{event.participants}/{event.maxParticipants}</span>
                      </div>
                    )}
                  </div>
                </div>
              ))}
          </CardContent>
        </Card>

        <Card className="bg-white/80 backdrop-blur-sm border-gray-300 shadow-md">
          <CardHeader>
            <CardTitle className="text-xl text-gray-900">Event Types</CardTitle>
            <CardDescription>Understanding our event categories</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-start space-x-3">
              <div className="p-2 bg-red-100 rounded-lg">
                <Trophy className="w-5 h-5 text-red-700" />
              </div>
              <div>
                <h4 className="font-semibold text-gray-900">Competitions</h4>
                <p className="text-sm text-gray-700">Official HFT competitions with scoring and prizes</p>
              </div>
            </div>
            <div className="flex items-start space-x-3">
              <div className="p-2 bg-gray-100 rounded-lg">
                <Target className="w-5 h-5 text-gray-700" />
              </div>
              <div>
                <h4 className="font-semibold text-gray-900">Training Sessions</h4>
                <p className="text-sm text-gray-700">Skill development and technique improvement</p>
              </div>
            </div>
            <div className="flex items-start space-x-3">
              <div className="p-2 bg-gray-100 rounded-lg">
                <Users className="w-5 h-5 text-gray-700" />
              </div>
              <div>
                <h4 className="font-semibold text-gray-900">Social Events</h4>
                <p className="text-sm text-gray-700">Family-friendly activities and club gatherings</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}