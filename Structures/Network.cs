﻿using SVN.Core.Collections;
using SVN.Core.Linq;
using SVN.Math2;
using SVN.NeuralNetwork.Helpers;
using SVN.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SVN.NeuralNetwork.Structures
{
    public class Network
    {
        private const string DATA_SEPARATOR = "\r\n";
        private static Random Random { get; } = new Random(DateTime.Now.Millisecond);
        private List<Layer> Layers { get; } = new List<Layer>();
        private int Epoch { get; set; }
        private double Error { get; set; } = .5;
        private double ErrorApproximation { get; set; } = .5;

        public int InputLayerLength { get; set; } = 1;
        public int HiddenLayerLength { get; set; }
        public int OutputLayerLength { get; set; } = 1;
        public int HiddenLayerAmount { get; set; }
        public double InitialeWeightRange { get; set; }

        private double Alpha
        {
            get => 1 - this.ErrorApproximation;
        }

        private double Eta
        {
            get => Math.Pow(this.ErrorApproximation, 2);
        }

        public double ErrorPercentage
        {
            get => this.ErrorApproximation * 100;
        }

        public bool HasLearnedEnough
        {
            get => this.ErrorPercentage < 1;
        }

        public int[] Results
        {
            get => this.GetOutputValues().Select(x => x.RoundToInt()).ToArray();
        }

        public int ResultMaxIndex
        {
            get
            {
                var results = this.GetOutputValues().ToList();
                var resultMax = results.Max();
                var index = results.IndexOf(resultMax);
                return index;
            }
        }

        public Network(double initialeWeightRange = .5)
        {
            this.InitialeWeightRange = initialeWeightRange;
        }

        public void Import(string data)
        {
            var items = data.Split(Enumerable.Range(1, 3).Select(x => Network.DATA_SEPARATOR).Join(string.Empty)).ToList();

            foreach (var item in items)
            {
                var index = items.IndexOf(item);
                var layer = this.Layers.ElementAt(index);
                layer.Import(item, Network.DATA_SEPARATOR);
            }
        }

        public void ImportFromFile(string path)
        {
            if (File.Exists(path))
            {
                var data = File.ReadAllText(path);
                this.Import(data);
            }
        }

        public string Export()
        {
            return this.Layers.Select(x => x.Export(Network.DATA_SEPARATOR)).Join(Enumerable.Range(1, 3).Select(x => Network.DATA_SEPARATOR).Join(string.Empty));
        }

        public void ExportToFile(string path)
        {
            var directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = this.Export();
            File.WriteAllText(path, data);
        }

        public void Initialize()
        {
            this.ClearLayers();
            this.AddInputLayer(this.InputLayerLength);
            for (var i = 1; i <= this.HiddenLayerAmount; i++)
            {
                //this.AddHiddenLayer(this.InputLayerLength * 2 / 3 + this.OutputLayerLength);
                this.AddHiddenLayer(this.HiddenLayerLength);
            }
            this.AddOutputLayer(this.OutputLayerLength);
            this.Connect();
        }

        public void Initialize(TrainingData data, int hiddenLayers = 2)
        {
            this.ClearLayers();
            this.AddInputLayer(data.InputLength);
            for (var i = 1; i <= hiddenLayers; i++)
            {
                this.AddHiddenLayer(data.InputLength * 2 / 3 + data.OutputLength);
            }
            this.AddOutputLayer(data.OutputLength);
            this.Connect();
        }

        internal static double GetRandomNumber(double min, double max)
        {
            var diff = Math.Abs(max - min);
            var rnd = Network.Random.NextDouble();
            return min + diff * rnd;
        }

        private void ClearLayers()
        {
            this.Layers.Clear();
        }

        private void AddInputLayer(int neurons)
        {
            var layer = new LayerInput(neurons);
            this.Layers.Add(layer);
        }

        private void AddHiddenLayer(int neurons)
        {
            var layer = new LayerHidden(neurons);
            this.Layers.Add(layer);
        }

        private void AddOutputLayer(int neurons)
        {
            var layer = new LayerOutput(neurons);
            this.Layers.Add(layer);
        }

        private void Connect()
        {
            var layer1 = default(Layer);
            foreach (var layer2 in this.Layers)
            {
                if (layer1 != null)
                {
                    Layer.Connect(layer1, layer2, this.InitialeWeightRange);
                }
                layer1 = layer2;
            }
        }

        private void SetOutputValues(params double[] values)
        {
            foreach (var layer in this.Layers.Where(x => x is LayerInput))
            {
                layer.SetOutputValues(values);
            }
        }

        private void CalculateValues(params double[] values)
        {
            foreach (var layer in this.Layers.Where(x => x is LayerHidden || x is LayerOutput))
            {
                layer.CalculateValues();
            }
        }

        private void CalculateError(params double[] values)
        {
            foreach (var layer in this.Layers.Where(x => x is LayerOutput))
            {
                this.Error = layer.GetError(values).Value;
                this.ErrorApproximation = this.ErrorApproximation.Approach(this.Error);
            }
        }

        private void CalculateGradients(params double[] values)
        {
            foreach (var layer in this.Layers.Invert())
            {
                layer.CalculateGradients(values);
            }
        }

        private void UpdateWeights()
        {
            foreach (var layer in this.Layers.Invert())
            {
                layer.UpdateWeights(this.Alpha, this.Eta);
            }
        }

        private IEnumerable<double> GetOutputValues()
        {
            foreach (var layer in this.Layers.Where(x => x is LayerOutput))
            {
                foreach (var value in layer.GetOutputValues().ToList())
                {
                    yield return value;
                }
            }
        }

        private void FeedForward(params double[] values)
        {
            this.SetOutputValues(values);
            this.CalculateValues(values);
        }

        private void BackPropagation(params double[] values)
        {
            this.CalculateError(values);
            this.CalculateGradients(values);
            this.UpdateWeights();
            this.Epoch++;
        }

        public void TrainOnce(TrainingData data)
        {
            var (input, output) = data.Random;
            this.FeedForward(input);
            this.BackPropagation(output);
        }

        public void TrainFull(TrainingData data, TimeSpan sleepTimePerEpoch = default(TimeSpan))
        {
            TaskContainer.Run(() =>
            {
                while (!this.HasLearnedEnough)
                {
                    var (input, output) = data.Random;

                    this.FeedForward(input);
                    this.BackPropagation(output);

                    if (sleepTimePerEpoch != default(TimeSpan))
                    {
                        Thread.Sleep(sleepTimePerEpoch);
                    }
                }
            });
        }

        public override string ToString()
        {
            var layers = this.Layers.Select(x => $"{x}").Join("\n\n");
            var epoch = $"Epoch: {this.Epoch}";
            var alpha = $"Alpha: {this.Alpha:N5}";
            var eta = $"Eta: {this.Eta:N5}";
            var error = $"Error: {this.Error:N5}";
            var errorApproximation = $"ErrorApproximation: {this.ErrorApproximation:N5}";

            return $"{layers}\n\n{epoch}\n{alpha}\n{eta}\n{error}\n{errorApproximation}";
        }
    }
}