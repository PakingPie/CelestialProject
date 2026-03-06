/*
    Volumetric nebula rendered in tiles. Use mouse to move camera.
    Wait for blue noise texture to load.
*/

void mainImage( out vec4 fragColor, in vec2 fragCoord ){
    vec2 uv = fragCoord/iResolution.xy;
    vec3 col = texture(iChannel1, uv).rgb;
    fragColor = vec4(col, 1.0);
}