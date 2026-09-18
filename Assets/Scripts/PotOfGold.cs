using UnityEngine;

public sealed class PotOfGold : MonoBehaviour
{
    public System.Action OnCollected;

    public static PotOfGold Spawn(Vector3 position)
    {
        var root = new GameObject("PotOfGold");
        root.transform.position = position + Vector3.up * 0.05f;

        var potMat = Materials.Lit(new Color(0.18f, 0.1f, 0.05f), 0.35f, 0.4f);
        var goldMat = Materials.Lit(new Color(1f, 0.78f, 0.15f), 0.85f, 0.7f);
        var rimMat = Materials.Lit(new Color(0.85f, 0.62f, 0.12f), 0.7f, 0.65f);

        var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "Pot";
        pot.transform.SetParent(root.transform, false);
        pot.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        pot.transform.localScale = new Vector3(0.95f, 0.38f, 0.95f);
        pot.GetComponent<Renderer>().sharedMaterial = potMat;

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Rim";
        rim.transform.SetParent(root.transform, false);
        rim.transform.localPosition = new Vector3(0f, 0.74f, 0f);
        rim.transform.localScale = new Vector3(1.05f, 0.05f, 1.05f);
        rim.GetComponent<Renderer>().sharedMaterial = rimMat;
        Object.Destroy(rim.GetComponent<Collider>());

        var rng = new System.Random(99);
        for (int i = 0; i < 14; i++)
        {
            var coin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.name = "Coin";
            coin.transform.SetParent(root.transform, false);
            float ang = i * 24f * Mathf.Deg2Rad;
            float r = 0.12f + (i % 4) * 0.07f;
            coin.transform.localPosition = new Vector3(Mathf.Cos(ang) * r, 0.82f + (i % 3) * 0.1f, Mathf.Sin(ang) * r);
            coin.transform.localScale = Vector3.one * (0.16f + (float)rng.NextDouble() * 0.08f);
            coin.GetComponent<Renderer>().sharedMaterial = goldMat;
            Object.Destroy(coin.GetComponent<Collider>());
        }

        var lightGo = new GameObject("GoldLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.82f, 0.3f);
        light.intensity = 3.2f;
        light.range = 8f;

        var sparkles = new GameObject("Sparkles");
        sparkles.transform.SetParent(root.transform, false);
        sparkles.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var ps = sparkles.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new Color(1f, 0.85f, 0.25f);
        main.startSize = 0.08f;
        main.startLifetime = 1.4f;
        main.startSpeed = 0.55f;
        main.maxParticles = 40;
        var emission = ps.emission;
        emission.rateOverTime = 16f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.45f;
        var renderer = sparkles.GetComponent<ParticleSystemRenderer>();
        var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Sprites/Default");
        renderer.material = new Material(particleShader);

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.35f;
        trigger.center = new Vector3(0f, 0.6f, 0f);
        root.AddComponent<Rigidbody>().isKinematic = true;

        return root.AddComponent<PotOfGold>();
    }

    void Update()
    {
        transform.Rotate(0f, 18f * Time.deltaTime, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<FarmWalkerController>() != null)
            OnCollected?.Invoke();
    }
}
