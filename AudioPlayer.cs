using System;
using NAudio.Wave;

namespace Tetris.Audio
{
    public class AudioPlayer : IDisposable
    {
        private WaveOutEvent outputDevice;
        private AudioFileReader audioFile;

        public AudioPlayer(string filePath)
        {
            outputDevice = new WaveOutEvent();
            audioFile = new AudioFileReader(filePath);
            outputDevice.Init(audioFile);
        }

        public void Play()
        {
            // Reset position to start if it already played
            audioFile.Position = 0;
            outputDevice.Play();
        }

        public void Stop()
        {
            outputDevice.Stop();
        }

        public float Volume
        {
            get => audioFile.Volume;
            set => audioFile.Volume = value;
        }

        public void Dispose()
        {
            outputDevice?.Dispose();
            audioFile?.Dispose();
        }
    }
    public static class AduioLibrary
    {
        // this is dog shit
        public static AudioPlayer rotateSound;
        public static AudioPlayer tetrisSound;
        public static AudioPlayer collisionSound;
        public static AudioPlayer clearSound;
        public static void Mute(bool mute)
        {
            if (mute)
            {
                rotateSound.Volume = 0;
                tetrisSound.Volume = 0;
                collisionSound.Volume = 0;
                clearSound.Volume = 0;
            }
            else
            {
                rotateSound.Volume = 1;
                tetrisSound.Volume = 1;
                collisionSound.Volume = 1;
                clearSound.Volume = 1;
            }
        }
        public static void Init()
        {
            rotateSound = new AudioPlayer("Asset\\plik.wav");
            tetrisSound = new AudioPlayer("Asset\\tetris.wav");
            clearSound = new AudioPlayer("Asset\\shake.wav");
            collisionSound = new AudioPlayer("Asset\\hit_bass.wav");
        }

    }
}