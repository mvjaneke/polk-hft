'use client'

import { useState } from 'react'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { X, ChevronLeft, ChevronRight, Camera, Users, Trophy, Target } from 'lucide-react'

interface GalleryImage {
  id: string
  src: string
  alt: string
  title: string
  category: 'competition' | 'training' | 'social' | 'awards'
  description: string
}

const galleryImages: GalleryImage[] = [
  {
    id: '1',
    src: '/gallery-shooter-1.jpg',
    alt: 'HFT shooter in prone position during competition',
    title: 'Precision Shooting',
    category: 'competition',
    description: 'Member demonstrating perfect prone position technique during monthly competition'
  },
  {
    id: '2',
    src: '/gallery-shooter-2.jpg',
    alt: 'HFT shooter in kneeling position',
    title: 'Steady Aim',
    category: 'training',
    description: 'Training session focusing on kneeling position stability and accuracy'
  },
  {
    id: '3',
    src: '/gallery-targets.jpg',
    alt: 'HFT metal targets at various distances',
    title: 'Target Setup',
    category: 'competition',
    description: 'Professional target placement at various distances up to 42 meters'
  },
  {
    id: '4',
    src: '/gallery-club.jpg',
    alt: 'Club members socializing at outdoor event',
    title: 'Club Community',
    category: 'social',
    description: 'Members enjoying fellowship and sharing experiences at our monthly gathering'
  },
  {
    id: '5',
    src: '/gallery-awards.jpg',
    alt: 'Awards ceremony with trophy presentation',
    title: 'Champions Celebrated',
    category: 'awards',
    description: 'Annual championship awards ceremony recognizing outstanding achievements'
  },
  {
    id: '6',
    src: '/gallery-training.jpg',
    alt: 'Coach providing instruction to shooter',
    title: 'Expert Coaching',
    category: 'training',
    description: 'Personalized coaching sessions to improve shooting techniques'
  },
  {
    id: '7',
    src: '/hft-competition.jpg',
    alt: 'HFT competition in natural woodland setting',
    title: 'Natural Course',
    category: 'competition',
    description: 'Competition underway on our beautiful woodland course'
  },
  {
    id: '8',
    src: '/hft-course.jpg',
    alt: 'Hunter Field Target shooting course',
    title: 'Our Range',
    category: 'competition',
    description: 'Overview of our main HFT course with natural terrain features'
  }
]

export default function Gallery() {
  const [selectedImage, setSelectedImage] = useState<GalleryImage | null>(null)
  const [currentImageIndex, setCurrentImageIndex] = useState(0)
  const [selectedCategory, setSelectedCategory] = useState<string>('all')

  const categories = [
    { id: 'all', label: 'All Photos', icon: <Camera className="w-4 h-4" /> },
    { id: 'competition', label: 'Competitions', icon: <Trophy className="w-4 h-4" /> },
    { id: 'training', label: 'Training', icon: <Target className="w-4 h-4" /> },
    { id: 'social', label: 'Social', icon: <Users className="w-4 h-4" /> },
    { id: 'awards', label: 'Awards', icon: <Trophy className="w-4 h-4" /> }
  ]

  const filteredImages = selectedCategory === 'all' 
    ? galleryImages 
    : galleryImages.filter(img => img.category === selectedCategory)

  const openLightbox = (image: GalleryImage) => {
    setSelectedImage(image)
    setCurrentImageIndex(filteredImages.findIndex(img => img.id === image.id))
  }

  const closeLightbox = () => {
    setSelectedImage(null)
  }

  const navigateImage = (direction: 'prev' | 'next') => {
    if (!selectedImage) return
    
    let newIndex = currentImageIndex
    if (direction === 'prev') {
      newIndex = currentImageIndex > 0 ? currentImageIndex - 1 : filteredImages.length - 1
    } else {
      newIndex = currentImageIndex < filteredImages.length - 1 ? currentImageIndex + 1 : 0
    }
    
    setCurrentImageIndex(newIndex)
    setSelectedImage(filteredImages[newIndex])
  }

  const getCategoryColor = (category: string) => {
    switch (category) {
      case 'competition':
        return 'bg-red-100 text-red-800 border-red-200'
      case 'training':
        return 'bg-gray-100 text-gray-800 border-gray-200'
      case 'social':
        return 'bg-gray-100 text-gray-800 border-gray-200'
      case 'awards':
        return 'bg-gray-100 text-gray-800 border-gray-200'
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200'
    }
  }

  return (
    <div className="space-y-8">
      {/* Category Filter */}
      <div className="flex flex-wrap gap-2 justify-center">
        {categories.map(category => (
          <Button
            key={category.id}
            variant={selectedCategory === category.id ? 'default' : 'outline'}
            onClick={() => setSelectedCategory(category.id)}
            className={`flex items-center space-x-2 ${
              selectedCategory === category.id 
                ? 'bg-red-700 text-white border-red-700' 
                : 'border-gray-300 text-gray-700 hover:bg-gray-50'
            }`}
          >
            {category.icon}
            <span>{category.label}</span>
          </Button>
        ))}
      </div>

      {/* Gallery Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
        {filteredImages.map((image) => (
          <Card 
            key={image.id} 
            className="group cursor-pointer overflow-hidden bg-white/80 backdrop-blur-sm border-gray-300 shadow-md hover:shadow-xl transition-all duration-300 hover:scale-105"
            onClick={() => openLightbox(image)}
          >
            <div className="relative aspect-[4/3] overflow-hidden">
              <img
                src={image.src}
                alt={image.alt}
                className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-110"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300">
                <div className="absolute bottom-0 left-0 right-0 p-4 text-white">
                  <h3 className="font-semibold text-lg">{image.title}</h3>
                  <p className="text-sm opacity-90">{image.description}</p>
                </div>
              </div>
            </div>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <h3 className="font-semibold text-gray-900">{image.title}</h3>
                <Badge className={getCategoryColor(image.category)}>
                  {image.category}
                </Badge>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Lightbox */}
      {selectedImage && (
        <div className="fixed inset-0 bg-black/90 z-50 flex items-center justify-center p-4" onClick={closeLightbox}>
          <div className="relative max-w-6xl w-full">
            {/* Close Button */}
            <Button
              variant="ghost"
              size="icon"
              className="absolute top-4 right-4 text-white hover:bg-white/20 z-10"
              onClick={closeLightbox}
            >
              <X className="w-6 h-6" />
            </Button>

            {/* Navigation Buttons */}
            <Button
              variant="ghost"
              size="icon"
              className="absolute left-4 top-1/2 -translate-y-1/2 text-white hover:bg-white/20 z-10"
              onClick={(e) => {
                e.stopPropagation()
                navigateImage('prev')
              }}
            >
              <ChevronLeft className="w-8 h-8" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="absolute right-4 top-1/2 -translate-y-1/2 text-white hover:bg-white/20 z-10"
              onClick={(e) => {
                e.stopPropagation()
                navigateImage('next')
              }}
            >
              <ChevronRight className="w-8 h-8" />
            </Button>

            {/* Image Container */}
            <div className="bg-white rounded-lg overflow-hidden">
              <div className="relative aspect-[16/10]">
                <img
                  src={selectedImage.src}
                  alt={selectedImage.alt}
                  className="w-full h-full object-contain"
                />
              </div>
              <div className="p-6">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-2xl font-bold text-green-900">{selectedImage.title}</h3>
                  <Badge className={getCategoryColor(selectedImage.category)}>
                    {selectedImage.category}
                  </Badge>
                </div>
                <p className="text-gray-700 mb-4">{selectedImage.description}</p>
                <div className="flex items-center justify-between text-sm text-gray-500">
                  <span>{currentImageIndex + 1} of {filteredImages.length}</span>
                  <div className="flex space-x-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={(e) => {
                        e.stopPropagation()
                        navigateImage('prev')
                      }}
                      className="border-gray-300 text-gray-700 hover:bg-gray-50"
                    >
                      Previous
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={(e) => {
                        e.stopPropagation()
                        navigateImage('next')
                      }}
                      className="border-gray-300 text-gray-700 hover:bg-gray-50"
                    >
                      Next
                    </Button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}