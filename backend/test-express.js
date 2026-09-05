const express = require('express');

const app = express();

const router1 = express.Router();
router1.use((req, res, next) => {
  console.log('router1 middleware');
  next();
});
router1.post('/me', (req, res) => res.json({ msg: 'router1' }));

const router2 = express.Router();
router2.use((req, res, next) => {
  console.log('router2 middleware');
  next();
});
router2.post('/me/progression/commit', (req, res) => res.json({ msg: 'router2' }));

app.use('/character', router1);
app.use('/character', router2);

app.use((req, res) => {
  console.log('404 catch-all');
  res.status(404).send('Not Found');
});

const request = require('supertest');
request(app)
  .post('/character/me/progression/commit')
  .expect(200)
  .end((err, res) => {
    if (err) console.error(err);
    console.log(res.body);
    process.exit(0);
  });
