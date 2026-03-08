const express = require('express');
const path = require('path');
const cors = require('cors');
const app = express();
const port = 3003;

app.use(cors());
app.use(express.static(path.join(__dirname, 'public')));

app.get('/api/movies', (req, res) => {
    // Mock list with movies and series from actual sources
    res.json([
        { id: 'tt1375666', title: 'Inception', type: 'movie', url: 'https://vidsrc.xyz/embed/movie/tt1375666', poster: 'https://image.tmdb.org/t/p/w500/oYuS0VV65rP7Bf2S6A0HwzB99Bv.jpg' },
        { id: 'tt0133093', title: 'The Matrix', type: 'movie', url: 'https://vidsrc.me/embed/movie/tt0133093', poster: 'https://image.tmdb.org/t/p/w500/f89U3Y9S9SdyzqH9GvY870S9biC.jpg' },
        { id: 'tt0816692', title: 'Interstellar', type: 'movie', url: 'https://vidsrc.pro/embed/movie/tt0816692', poster: 'https://image.tmdb.org/t/p/w500/gEU2QniE6E77NI6lCU6MxlSaba7.jpg' },
        { id: 'tt0944947', title: 'Game of Thrones', type: 'series', url: 'https://vidsrc.xyz/embed/tv/tt0944947/1/1', poster: 'https://image.tmdb.org/t/p/w500/7WsyChvRStvT0tO2EOvNi6P90Ym.jpg' },
        { id: 'tt0903747', title: 'Breaking Bad', type: 'series', url: 'https://vidsrc.me/embed/tv?imdb=tt0903747&season=1&episode=1', poster: 'https://image.tmdb.org/t/p/w500/ggm8bbub6o6S1M0Y7ZpbiC1v6ia.jpg' },
        { id: 'tt4154756', title: 'Avengers: Infinity War', type: 'movie', url: 'https://autoembed.cc/embed/movie/tt4154756', poster: 'https://image.tmdb.org/t/p/w500/7WsyChvRStvT0tO2EOvNi6P90Ym.jpg' },
        { id: 'tt1119646', title: 'The Flash', type: 'movie', url: 'https://vidsrc.xyz/embed/movie/tt1119646', poster: 'https://image.tmdb.org/t/p/w500/rktD8oRjS0XmRp3dnVqM0p0H9vt.jpg' },
        { id: 'tt15348164', title: 'Oppenheimer', type: 'movie', url: 'https://vidsrc.xyz/embed/movie/tt15348164', poster: 'https://image.tmdb.org/t/p/w500/8GxvA9zDZp0GmwvRhEPca27UCL0.jpg' },
        { id: 'tt0111161', title: 'The Shawshank Redemption', type: 'movie', url: 'https://vidsrc.xyz/embed/movie/tt0111161', poster: 'https://image.tmdb.org/t/p/w500/q6y0GoSdyYpRMCBTbsvAyZp1v85.jpg' }
    ]);
});

app.listen(port, () => console.log(`Nexus server running on port ${port}`));
