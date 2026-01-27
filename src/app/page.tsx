'use client'

import { useState, useEffect } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Target, Trophy, Users, Clock, MapPin, Phone, Mail, Star, Shield, Crosshair, Trees, Mountain, Wind, Calendar, Camera, Menu, X, ArrowUp } from 'lucide-react'
import EventsCalendar from '@/components/EventsCalendar'
import Gallery from '@/components/Gallery'

export default function Home() {
  const [isLoaded, setIsLoaded] = useState(false)
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false)
  const [showBackToTop, setShowBackToTop] = useState(false)

  useEffect(() => {
    setIsLoaded(true)
    
    const handleScroll = () => {
      setShowBackToTop(window.scrollY > 300)
    }

    window.addEventListener('scroll', handleScroll)
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  const scrollToSection = (sectionId: string) => {
    const element = document.getElementById(sectionId)
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
    setIsMobileMenuOpen(false) // Close mobile menu after navigation
  }

  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' })
    setIsMobileMenuOpen(false) // Close mobile menu if open
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 via-gray-100 to-gray-200 text-gray-900">
      {/* Natural Background Elements */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none opacity-10">
        <div className="absolute top-20 left-20 w-64 h-64 bg-red-600 rounded-full blur-3xl"></div>
        <div className="absolute top-40 right-20 w-80 h-80 bg-gray-600 rounded-full blur-3xl"></div>
        <div className="absolute -bottom-8 left-40 w-72 h-72 bg-red-700 rounded-full blur-3xl"></div>
      </div>

      {/* Skip to main content for accessibility */}
      <a href="#main-content" className="sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 bg-red-600 text-white px-4 py-2 rounded">
        Skip to main content
      </a>

      {/* Navigation */}
      <nav role="navigation" aria-label="Main navigation" className="sticky top-0 z-50 border-b border-gray-300 bg-white/90 backdrop-blur-sm shadow-md">
        <div className="container mx-auto px-4 py-4">
          <div className="flex justify-between items-center">
            <div className="flex items-center space-x-2">
              <Crosshair className="h-8 w-8 text-red-700" aria-hidden="true" />
              <span className="text-xl font-bold text-gray-900">
                PEARC
              </span>
            </div>
            
            {/* Desktop Navigation */}
            <div className="hidden md:flex space-x-6">
              <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50" onClick={() => scrollToSection('home')}>Home</Button>
              <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50" onClick={() => scrollToSection('about')}>About HFT</Button>
              <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50" onClick={() => scrollToSection('gallery')}>Gallery</Button>
              <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50" onClick={() => scrollToSection('events')}>Events</Button>
              <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50" onClick={() => scrollToSection('membership')}>Membership</Button>
              <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50" onClick={() => scrollToSection('contact')}>Contact</Button>
            </div>

            {/* Mobile Menu Button */}
            <Button
              variant="ghost"
              size="icon"
              className="md:hidden text-gray-700 hover:text-red-700 hover:bg-red-50"
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              aria-label="Toggle mobile menu"
            >
              {isMobileMenuOpen ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
            </Button>
          </div>

          {/* Mobile Navigation Menu */}
          {isMobileMenuOpen && (
            <div className="md:hidden mt-4 pt-4 border-t border-gray-300">
              <div className="flex flex-col space-y-2">
                <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50 justify-start" onClick={() => scrollToSection('home')}>Home</Button>
                <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50 justify-start" onClick={() => scrollToSection('about')}>About HFT</Button>
                <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50 justify-start" onClick={() => scrollToSection('gallery')}>Gallery</Button>
                <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50 justify-start" onClick={() => scrollToSection('events')}>Events</Button>
                <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50 justify-start" onClick={() => scrollToSection('membership')}>Membership</Button>
                <Button variant="ghost" className="text-gray-700 hover:text-red-700 hover:bg-red-50 justify-start" onClick={() => scrollToSection('contact')}>Contact</Button>
              </div>
            </div>
          )}
        </div>
      </nav>

      {/* Hero Section */}
      <main id="main-content">
        <section id="home" className="relative z-10 container mx-auto px-4 py-20" aria-labelledby="hero-heading">
          <div className="grid lg:grid-cols-2 gap-12 items-center">
            <div className={`space-y-8 transition-all duration-1000 ${isLoaded ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-10'}`}>
              <div className="space-y-4">
                <Badge className="bg-red-700 text-white border-0 px-4 py-2 text-sm">
                  <Trees className="w-4 h-4 mr-2" />
                  Outdoor Shooting Excellence
                </Badge>
                <h1 id="hero-heading" className="text-5xl md:text-7xl font-bold text-gray-900">
                  Pretoria East
                  <br />
                  Air Rifle Club
                </h1>
                <p className="text-xl md:text-2xl text-gray-700 max-w-3xl">
                  Experience Hunter Field Target in the Natural Beauty of South Africa
                </p>
              </div>
              
              <div className="flex flex-col sm:flex-row gap-4">
                <Button size="lg" className="bg-red-700 hover:bg-red-800 text-white border-0 px-8 py-6 text-lg" onClick={() => scrollToSection('events')}>
                  <Trophy className="w-5 h-5 mr-2" aria-hidden="true" />
                  Join Competition
                </Button>
                <Button size="lg" variant="outline" className="border-red-700 text-red-700 hover:bg-red-50 px-8 py-6 text-lg" onClick={() => scrollToSection('membership')}>
                  <Users className="w-5 h-5 mr-2" aria-hidden="true" />
                  Become Member
                </Button>
              </div>
            </div>
            
            <div className={`relative transition-all duration-1000 ${isLoaded ? 'opacity-100 translate-x-0' : 'opacity-0 translate-x-10'}`} style={{transitionDelay: '300ms'}}>
              <div className="relative">
                <div className="absolute inset-0 bg-red-700/10 rounded-2xl blur-xl"></div>
                <img 
                  src="/hft-course.jpg" 
                  alt="Outdoor Hunter Field Target shooting course in natural woodland setting with realistic targets"
                  className="relative rounded-2xl w-full h-auto shadow-2xl border border-gray-300"
                />
              </div>
            </div>
          </div>
        </section>

        {/* Features Grid */}
        <section id="about" className="relative z-10 container mx-auto px-4 py-20">
          <h2 className="text-4xl font-bold text-center mb-12 text-green-900">
            Why Choose PEARC?
          </h2>
          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
            {[
              {
                icon: <Target className="w-8 h-8 text-green-700" />,
                title: "Precision Shooting",
                description: "Develop your marksmanship skills in natural outdoor conditions"
              },
              {
                icon: <Shield className="w-8 h-8 text-green-600" />,
                title: "Safety First",
                description: "Comprehensive safety protocols for all skill levels"
              },
              {
                icon: <Star className="w-8 h-8 text-amber-700" />,
                title: "Competitive Spirit",
                description: "Regular tournaments with prizes and recognition"
              },
              {
                icon: <Users className="w-8 h-8 text-green-700" />,
                title: "Expert Coaching",
                description: "Professional instructors for beginners to advanced shooters"
              }
            ].map((feature, index) => (
              <Card key={index} className="bg-white/80 backdrop-blur-sm border-green-200 hover:bg-green-50 transition-all duration-300 hover:scale-105 group shadow-md">
                <CardHeader className="text-center">
                  <div className="flex justify-center mb-4 group-hover:scale-110 transition-transform">
                    {feature.icon}
                  </div>
                  <CardTitle className="text-green-900 text-lg">{feature.title}</CardTitle>
                </CardHeader>
                <CardContent>
                  <CardDescription className="text-center text-gray-700 text-sm">
                    {feature.description}
                  </CardDescription>
                </CardContent>
              </Card>
            ))}
          </div>
          
          {/* Equipment Showcase */}
          <div className="mt-16 bg-white/80 backdrop-blur-sm rounded-2xl p-8 border border-green-200 shadow-md">
            <div className="grid lg:grid-cols-2 gap-12 items-center">
              <div className="space-y-6">
                <h3 className="text-3xl font-bold text-green-900">
                  Professional HFT Equipment
                </h3>
                <div className="space-y-4 text-gray-700">
                  <p>
                    Train with professional-grade air rifles featuring high-quality telescopic scopes, 
                    ergonomic designs, and precision engineering. Our equipment is maintained to the highest standards.
                  </p>
                  <ul className="space-y-2">
                    <li className="flex items-center space-x-2">
                      <div className="w-2 h-2 bg-green-600 rounded-full"></div>
                      <span>High-precision telescopic scopes with parallax adjustment</span>
                    </li>
                    <li className="flex items-center space-x-2">
                      <div className="w-2 h-2 bg-amber-600 rounded-full"></div>
                      <span>Adjustable stocks for perfect fit and comfort</span>
                    </li>
                    <li className="flex items-center space-x-2">
                      <div className="w-2 h-2 bg-green-700 rounded-full"></div>
                      <span>Match-grade barrels for consistent accuracy</span>
                    </li>
                  </ul>
                </div>
              </div>
              <div className="relative">
                <div className="absolute inset-0 bg-green-700/10 rounded-2xl blur-xl"></div>
                <img 
                  src="/hft-rifle.jpg" 
                  alt="Professional Hunter Field Target air rifle with telescopic scope"
                  className="relative rounded-2xl w-full h-auto shadow-2xl border border-green-200"
                />
              </div>
            </div>
          </div>
        </section>

        {/* Competition Showcase */}
        <section className="relative z-10 container mx-auto px-4 py-20">
          <div className="bg-white/80 backdrop-blur-sm rounded-2xl p-8 md:p-12 border border-green-200 shadow-md">
            <div className="grid lg:grid-cols-2 gap-12 items-center">
              <div className="space-y-6">
                <h2 className="text-4xl font-bold text-green-900">
                  Competition Arena
                </h2>
                <div className="space-y-4 text-gray-700">
                  <p>
                    Experience the thrill of HFT competition in our natural outdoor course. 
                    Manual scoring systems, realistic shooting positions, and professionally designed courses.
                  </p>
                  <p>
                    Join our monthly tournaments and compete with the best shooters in the region. 
                    Categories available for beginners, intermediate, and expert levels.
                  </p>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="bg-green-50 rounded-lg p-4 text-center border border-green-200">
                    <div className="text-2xl font-bold text-green-700">42m</div>
                    <div className="text-sm text-gray-600">Max Range</div>
                  </div>
                  <div className="bg-amber-50 rounded-lg p-4 text-center border border-amber-200">
                    <div className="text-2xl font-bold text-amber-700">30</div>
                    <div className="text-sm text-gray-600">Target Positions</div>
                  </div>
                </div>
              </div>
              <div className="relative">
                <div className="absolute inset-0 bg-amber-700/10 rounded-2xl blur-xl"></div>
                <img 
                  src="/hft-competition.jpg" 
                  alt="Hunter Field Target competition in natural outdoor woodland setting"
                  className="relative rounded-2xl w-full h-auto shadow-2xl border border-green-200"
                />
              </div>
            </div>
          </div>
        </section>

          {/* Gallery Section */}
        <section id="gallery" className="relative z-10 container mx-auto px-4 py-20">
          <div className="text-center mb-12">
            <Badge className="bg-red-700 text-white border-0 px-4 py-2 text-sm mb-4">
              <Camera className="w-4 h-4 mr-2" />
              Our Activities
            </Badge>
            <h2 className="text-4xl font-bold text-gray-900 mb-4">
              Gallery
            </h2>
            <p className="text-xl text-gray-700 max-w-3xl mx-auto">
              Explore our club activities, competitions, training sessions, and community events
            </p>
          </div>
          <Gallery />
        </section>

        {/* Events Calendar Section */}
        <section id="events" className="relative z-10 container mx-auto px-4 py-20">
          <div className="text-center mb-12">
            <Badge className="bg-red-700 text-white border-0 px-4 py-2 text-sm mb-4">
              <Calendar className="w-4 h-4 mr-2" />
              Stay Updated
            </Badge>
            <h2 className="text-4xl font-bold text-gray-900 mb-4">
              Events Calendar
            </h2>
            <p className="text-xl text-gray-700 max-w-3xl mx-auto">
              Join our competitions, training sessions, and social events throughout the year
            </p>
          </div>
          <EventsCalendar />
        </section>

        {/* About HFT Section */}
        <section className="relative z-10 container mx-auto px-4 py-20">
          <div className="bg-white/80 backdrop-blur-sm rounded-2xl p-8 md:p-12 border border-green-200 shadow-md">
            <div className="grid md:grid-cols-2 gap-12 items-center">
              <div className="space-y-6">
                <h2 className="text-4xl font-bold text-green-900">
                  What is Hunter Field Target?
                </h2>
                <div className="space-y-4 text-gray-700">
                  <p>
                    Hunter Field Target (HFT) is a challenging outdoor shooting discipline that simulates hunting scenarios in natural environments. 
                    Competitors must accurately shoot at metal targets placed at various distances up to 42 meters in realistic shooting positions.
                  </p>
                  <p>
                    Our club offers world-class HFT experience with manual scoring systems, natural woodland courses, 
                    and professional coaching for beginners to advanced shooters. Experience shooting in varied terrain and weather conditions.
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {['Outdoor', 'Natural', 'Precision', 'Patience', 'Technique', 'Sportsmanship'].map((skill) => (
                    <Badge key={skill} variant="secondary" className="bg-green-100 text-green-800 border-green-300">
                      {skill}
                    </Badge>
                  ))}
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                {[
                  { icon: <Crosshair />, label: "Accuracy", value: "95%+" },
                  { icon: <Users />, label: "Members", value: "150+" },
                  { icon: <Trophy />, label: "Events", value: "24/yr" },
                  { icon: <Mountain />, label: "Terrain", value: "Natural" }
                ].map((stat, index) => (
                  <Card key={index} className="bg-gradient-to-br from-green-50 to-amber-50 border-green-200">
                    <CardContent className="p-6 text-center">
                      <div className="flex justify-center mb-2 text-green-700">
                        {stat.icon}
                      </div>
                      <div className="text-2xl font-bold text-green-900">{stat.value}</div>
                      <div className="text-sm text-gray-600">{stat.label}</div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* Membership & Contact */}
        <section id="membership" className="relative z-10 container mx-auto px-4 py-20">
          <div className="grid md:grid-cols-2 gap-8">
            <Card className="bg-gradient-to-br from-green-700 to-green-600 text-white border-green-600 shadow-lg">
              <CardHeader>
                <CardTitle className="text-2xl text-white flex items-center">
                  <Users className="w-6 h-6 mr-2" />
                  Membership Options
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {[
                  { plan: "Basic", price: "R150/month", features: ["Access to practice range", "Basic equipment", "Monthly coaching"] },
                  { plan: "Premium", price: "R250/month", features: ["Full range access", "Advanced equipment", "Weekly coaching", "Competition entry"] },
                  { plan: "Elite", price: "R400/month", features: ["VIP access", "Personal coaching", "Equipment maintenance", "Priority booking"] }
                ].map((membership, index) => (
                  <div key={index} className="bg-white/10 rounded-lg p-4 border border-white/20">
                    <div className="flex justify-between items-center mb-2">
                      <h4 className="font-semibold text-white">{membership.plan}</h4>
                      <span className="text-amber-300 font-bold">{membership.price}</span>
                    </div>
                    <ul className="text-sm text-white/80 space-y-1">
                      {membership.features.map((feature, i) => (
                        <li key={i}>• {feature}</li>
                      ))}
                    </ul>
                  </div>
                ))}
              </CardContent>
            </Card>

            <Card className="bg-gradient-to-br from-amber-100 to-green-100 border-green-200 shadow-md">
              <CardHeader>
                <CardTitle className="text-2xl text-green-900">Get in Touch</CardTitle>
                <CardDescription className="text-gray-700">
                  Ready to start your HFT journey? Contact us today!
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center space-x-3 text-gray-700">
                  <MapPin className="w-5 h-5 text-green-700" />
                  <span>123 Shooting Range Road, Pretoria East</span>
                </div>
                <div className="flex items-center space-x-3 text-gray-700">
                  <Phone className="w-5 h-5 text-green-700" />
                  <span>+27 12 345 6789</span>
                </div>
                <div className="flex items-center space-x-3 text-gray-700">
                  <Mail className="w-5 h-5 text-green-700" />
                  <span>info@pearc.co.za</span>
                </div>
                <Separator className="bg-green-200" />
                <div className="space-y-2">
                  <h4 className="font-semibold text-green-900">Operating Hours:</h4>
                  <div className="text-sm text-gray-700 space-y-1">
                    <div>Monday - Friday: 14:00 - 19:00</div>
                    <div>Saturday - Sunday: 08:00 - 17:00</div>
                    <div>Public Holidays: 08:00 - 13:00</div>
                  </div>
                </div>
                <Button className="w-full bg-green-700 hover:bg-green-800 text-white border-0">
                  Join Now
                </Button>
              </CardContent>
            </Card>
          </div>
        </section>

        {/* Footer */}
        <footer id="contact" className="relative z-10 border-t border-gray-300 bg-gray-900 text-white">
          <div className="container mx-auto px-4 py-8">
            <div className="flex flex-col md:flex-row justify-between items-center">
              <div className="flex items-center space-x-2 mb-4 md:mb-0">
                <Crosshair className="h-6 w-6 text-red-400" />
                <span className="text-lg font-semibold">Pretoria East Air Rifle Club</span>
              </div>
              <div className="text-gray-400 text-sm">
                © 2024 PEARC. Outdoor Hunter Field Target shooting excellence.
              </div>
            </div>
          </div>
        </footer>
      </main>

      {/* Back to Top Button */}
      {showBackToTop && (
        <Button
          onClick={scrollToTop}
          size="lg"
          className="fixed bottom-8 right-8 z-50 bg-red-700 hover:bg-red-800 text-white border-0 shadow-lg hover:shadow-xl transition-all duration-300 hover:scale-110"
          aria-label="Back to top"
        >
          <ArrowUp className="w-5 h-5" />
        </Button>
      )}
    </div>
  )
}